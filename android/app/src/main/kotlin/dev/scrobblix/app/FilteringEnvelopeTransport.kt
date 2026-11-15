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
 * Bounded send budget shared by every transport instance: at most one send in
 * flight, a minimum interval between attempts, and a hard per-process attempt
 * cap. These bounds are process-local hygiene (they reset on restart): within a
 * process a crash loop cannot turn into a network or quota loop. Exceeding the
 * budget drops the report; there is no guaranteed delivery.
 */
object SendBudget {
    internal var maxAttemptsPerProcess=20
    internal var minIntervalMs=10_000L
    private val attempts=AtomicInteger(0)
    private val lastAttemptAtMs=AtomicLong(0)
    private val inFlight=AtomicBoolean(false)
    internal val attemptsSoFar:Int get()=attempts.get()
    fun tryAcquire():Boolean {
        if (!inFlight.compareAndSet(false,true)) return false
        val now=android.os.SystemClock.elapsedRealtime()
        if (attempts.get()>=maxAttemptsPerProcess || now-lastAttemptAtMs.get()<minIntervalMs) {
            inFlight.set(false)
            return false
        }
        attempts.incrementAndGet()
        lastAttemptAtMs.set(now)
        return true
    }
    fun release() { inFlight.set(false) }
    /** Test-only: reset shared state and bounds so cases stay isolated. */
    fun resetForTesting() {
        attempts.set(0)
        lastAttemptAtMs.set(0)
        inFlight.set(false)
        maxAttemptsPerProcess=20
        minIntervalMs=10_000L
    }
}

/**
 * The final transport, independent of every hook. Every envelope is rebuilt by
 * [EnvelopeGate] into the constants-only generic event and posted to the
 * configured DSN with a DIRECT SYNCHRONOUS request: this transport holds the
 * send budget until the actual HTTP exchange completes or times out, never
 * merely until an enqueue. Client reports cannot appear downstream because the
 * SDK's async transport (their only attach point) is not in the path; they are
 * additionally disabled at init.
 *
 * The original crash flush latch is bridged ONLY to a successfully completed
 * exchange (never to enqueue, cache or failure), so the uncaught handler
 * neither times out on success nor sees a false flush.
 *
 * Delivery is deliberately best-effort and bounded: [SendBudget] caps attempts,
 * a failure drops the report instead of retrying, and init pairs this with the
 * dedicated no-op envelope cache, so crash reports never accumulate. There is
 * no guaranteed crash delivery.
 */
class FilteringEnvelopeTransport(
    private val options:SentryOptions,
    private val requestDetails:RequestDetails,
    private val release:String,
    private val environment:String
):ITransport {
    private val serializer by lazy { JsonSerializer(SentryOptions.empty()) }

    override fun send(envelope:SentryEnvelope,hint:Hint) {
        val rebuilt=EnvelopeGate.rebuildToGenericEvent(envelope,release,environment) ?: return
        if (!SendBudget.tryAcquire()) return
        // Per-attempt correlation: the wire id only ever names the current attempt.
        lastWireEventId=null
        try {
            val connection=(requestDetails.url.openConnection() as HttpURLConnection).apply {
                requestMethod="POST"
                doOutput=true
                connectTimeout=options.connectionTimeoutMillis
                readTimeout=options.readTimeoutMillis
                for ((key,value) in requestDetails.headers) setRequestProperty(key,value)
                setRequestProperty("Content-Encoding","gzip")
                setRequestProperty("Content-Type","application/x-sentry-envelope")
                setRequestProperty("Accept","application/json")
                setRequestProperty("Connection","close")
            }
            lastWireEventId=rebuilt.header.eventId?.toString()
            connection.outputStream.use { raw -> GZIPOutputStream(raw).use { serializer.serialize(rebuilt,it) } }
            val code=connection.responseCode
            if (code in 200..299 && hint is DiskFlushNotification) hint.markFlushed()
            connection.disconnect()
        } catch (_:Throwable) {
            // Deliberate drop: crash reporting must never crash or block the app,
            // and a failed exchange never marks the crash latch.
        } finally {
            SendBudget.release()
        }
    }

    // Synchronous transport: nothing queued, nothing to flush, nothing to close.
    override fun flush(timeoutMillis:Long) {}
    override fun isHealthy():Boolean=true
    override fun getRateLimiter():RateLimiter?=null
    override fun close() {}
    override fun close(isRestarting:Boolean) {}
    companion object {
        /** Event id of the most recent rebuilt event actually handed to the wire.
         *  Receipt verification matches this id remotely; it is never a delivery
         *  claim, and it is cleared per attempt. */
        @Volatile var lastWireEventId:String?=null
            private set
        fun clearWireEventId() { lastWireEventId=null }
    }
}
