package dev.scrobblix.app

import io.sentry.Hint
import io.sentry.JsonSerializer
import io.sentry.RequestDetails
import io.sentry.SentryEnvelope
import io.sentry.SentryOptions
import io.sentry.cache.IEnvelopeCache
import io.sentry.hints.DiskFlushNotification
import io.sentry.transport.ITransport
import io.sentry.transport.RateLimiter
import java.net.HttpURLConnection
import java.util.concurrent.Executors
import java.util.concurrent.TimeUnit
import java.util.concurrent.atomic.AtomicBoolean
import java.util.concurrent.atomic.AtomicInteger
import java.util.concurrent.atomic.AtomicLong
import java.util.zip.GZIPOutputStream

/**
 * Dedicated no-op envelope cache. This is deliberately NOT the SDK's
 * NoOpEnvelopeCache sentinel: the Android initializer replaces the sentinel with
 * the on-disk AndroidEnvelopeCache whenever a cache path exists, so only a
 * dedicated class guarantees nothing is ever persisted. Crash reports are never
 * written to disk and never replayed.
 */
class ScrobblixNoOpEnvelopeCache:IEnvelopeCache {
    override fun store(envelope:SentryEnvelope,hint:Hint) {}
    // Deliberately false: a no-op cache stores nothing, so the SDK must never
    // treat storage (or a disk flush) as having happened.
    override fun storeEnvelope(envelope:SentryEnvelope,hint:Hint):Boolean=false
    override fun discard(envelope:SentryEnvelope) {}
    override fun iterator():MutableIterator<SentryEnvelope> = mutableListOf<SentryEnvelope>().iterator()
}

/**
 * Bounded send budget shared by every transport instance: at most one exchange
 * in flight (held for the whole actual HTTP exchange, never merely an enqueue),
 * a minimum interval between attempts, a hard per-process attempt cap and a
 * provider backoff cooldown. These bounds are process-local hygiene (they reset
 * on restart): within a process a crash loop cannot turn into a network or
 * quota loop. Exceeding the budget drops the report; there is no guaranteed
 * delivery.
 */
object SendBudget {
    internal var maxAttemptsPerProcess=20
    internal var minIntervalMs=10_000L
    internal var rateLimitCooldownMs=60_000L
    private val attempts=AtomicInteger(0)
    private val lastAttemptAtMs=AtomicLong(0)
    private val cooldownUntilMs=AtomicLong(0)
    private val inFlight=AtomicBoolean(false)
    internal val attemptsSoFar:Int get()=attempts.get()
    fun tryAcquire():Boolean {
        if (!inFlight.compareAndSet(false,true)) return false
        val now=android.os.SystemClock.elapsedRealtime()
        if (attempts.get()>=maxAttemptsPerProcess || now-lastAttemptAtMs.get()<minIntervalMs || now<cooldownUntilMs.get()) {
            inFlight.set(false)
            return false
        }
        attempts.incrementAndGet()
        lastAttemptAtMs.set(now)
        return true
    }
    /** Provider backoff: honor Retry-After instead of ignoring rate limits. */
    fun noteRateLimited(retryAfterHeader:String?) {
        val seconds=retryAfterHeader?.toLongOrNull()
        val ms=if (seconds!=null && seconds>0) seconds*1000 else rateLimitCooldownMs
        cooldownUntilMs.set(android.os.SystemClock.elapsedRealtime()+minOf(ms,300_000L))
    }
    fun release() { inFlight.set(false) }
    /** Test-only: reset shared state and bounds so cases stay isolated. */
    fun resetForTesting() {
        attempts.set(0)
        lastAttemptAtMs.set(0)
        cooldownUntilMs.set(0)
        inFlight.set(false)
        maxAttemptsPerProcess=20
        minIntervalMs=10_000L
        rateLimitCooldownMs=60_000L
    }
}

/**
 * Per-attempt wire correlation carried on the capture hint (never shared
 * mutable state): the transport writes the attempted event id and the exchange
 * outcome here, so the debug diagnostic reports only its own attempt.
 */
class WireAttemptResult {
    @Volatile var eventId:String?=null
    @Volatile var exchangeCompleted:Boolean=false
}

/**
 * The final transport, independent of every hook. Every envelope is rebuilt by
 * [EnvelopeGate] into the constants-only generic event and posted to the
 * configured DSN on a single bounded worker: the send budget is held for the
 * whole actual exchange (including cleanup), redirects are off, any response
 * body is drained before completion, a total deadline cancels stuck exchanges,
 * and provider rate limits are honored through a cooldown. Client reports have
 * no attach point because the SDK async transport is not in the path; they are
 * additionally disabled at init.
 *
 * The original crash flush latch is bridged ONLY to a successfully completed
 * exchange (never to enqueue, cache or failure), so the uncaught handler
 * neither times out on success nor sees a false flush.
 *
 * Delivery is deliberately best-effort and bounded: a failure drops the report
 * instead of retrying, and init pairs this with the dedicated no-op envelope
 * cache, so crash reports never accumulate. There is no guaranteed crash
 * delivery.
 */
class FilteringEnvelopeTransport(
    private val options:SentryOptions,
    private val requestDetails:RequestDetails,
    private val release:String,
    private val environment:String
):ITransport {
    private val serializer by lazy { JsonSerializer(SentryOptions.empty()) }
    private val worker=Executors.newSingleThreadExecutor { task -> Thread(task,"scrobblix-crash-sender").apply { isDaemon=true } }
    private val watchdog=Executors.newSingleThreadScheduledExecutor { task -> Thread(task,"scrobblix-crash-watchdog").apply { isDaemon=true } }

    override fun send(envelope:SentryEnvelope,hint:Hint) {
        val rebuilt=EnvelopeGate.rebuildToGenericEvent(envelope,release,environment) ?: return
        if (!SendBudget.tryAcquire()) return
        val result=hint.getAs(RESULT_HINT_KEY,WireAttemptResult::class.java)
        worker.execute {
            var connection:HttpURLConnection?=null
            // Total bound: covers DNS, connect, write, read and cleanup, not just
            // the socket timeouts.
            val cancel=watchdog.schedule({ try { connection?.disconnect() } catch (_:Throwable) {} },TOTAL_TIMEOUT_MS,TimeUnit.MILLISECONDS)
            try {
                val conn=(requestDetails.url.openConnection() as HttpURLConnection).apply {
                    requestMethod="POST"
                    doOutput=true
                    instanceFollowRedirects=false
                    connectTimeout=options.connectionTimeoutMillis
                    readTimeout=options.readTimeoutMillis
                    for ((key,value) in requestDetails.headers) setRequestProperty(key,value)
                    setRequestProperty("Content-Encoding","gzip")
                    setRequestProperty("Content-Type","application/x-sentry-envelope")
                    setRequestProperty("Accept","application/json")
                    setRequestProperty("Connection","close")
                }
                connection=conn
                conn.outputStream.use { raw -> GZIPOutputStream(raw).use { serializer.serialize(rebuilt,it) } }
                val code=conn.responseCode
                // Drain any response body so the socket closes cleanly.
                try { (if (code in 200..299) conn.inputStream else conn.errorStream)?.readBytes() } catch (_:Throwable) {}
                if (code==429) SendBudget.noteRateLimited(conn.getHeaderField("Retry-After"))
                result?.eventId=rebuilt.header.eventId?.toString()
                result?.exchangeCompleted=code in 200..299
                if (code in 200..299) (io.sentry.util.HintUtils.getSentrySdkHint(hint) as? DiskFlushNotification)?.markFlushed()
            } catch (_:Throwable) {
                // Deliberate drop: crash reporting must never crash or block the
                // app, and a failed exchange never marks the crash latch.
            } finally {
                cancel.cancel(false)
                try { connection?.disconnect() } catch (_:Throwable) {}
                SendBudget.release()
            }
        }
    }

    override fun flush(timeoutMillis:Long) {
        try { worker.submit {}.get(timeoutMillis,TimeUnit.MILLISECONDS) } catch (_:Throwable) {}
    }
    override fun isHealthy():Boolean=true
    override fun getRateLimiter():RateLimiter?=null
    override fun close() { shutdown() }
    override fun close(isRestarting:Boolean) { shutdown() }
    private fun shutdown() {
        worker.shutdown()
        watchdog.shutdown()
    }
    companion object {
        internal const val RESULT_HINT_KEY="scrobblix.wireAttemptResult"
        internal var TOTAL_TIMEOUT_MS=15_000L
    }
}
