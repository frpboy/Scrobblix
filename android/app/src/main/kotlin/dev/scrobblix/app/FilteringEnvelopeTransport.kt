package dev.scrobblix.app

import io.sentry.Hint
import io.sentry.JsonSerializer
import io.sentry.SentryEnvelope
import io.sentry.SentryItemType
import io.sentry.SentryOptions
import io.sentry.cache.IEnvelopeCache
import io.sentry.hints.DiskFlushNotification
import io.sentry.transport.ITransport
import io.sentry.transport.RateLimiter
import io.sentry.util.HintUtils
import java.io.ByteArrayOutputStream
import java.io.InputStream
import java.net.HttpURLConnection
import java.net.URI
import java.time.ZonedDateTime
import java.time.format.DateTimeFormatter
import java.util.concurrent.Executors
import java.util.concurrent.RejectedExecutionException
import java.util.concurrent.atomic.AtomicBoolean
import java.util.concurrent.atomic.AtomicInteger
import java.util.concurrent.atomic.AtomicLong

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
 * in flight (held for the whole actual HTTP exchange including cleanup, never
 * merely an enqueue), a minimum interval between attempts, a hard per-process
 * attempt cap and a provider backoff cooldown. These bounds are process-local
 * hygiene (they reset on restart): within a process a crash loop cannot turn
 * into a network or quota loop. Exceeding the budget drops the report; there
 * is no guaranteed delivery.
 *
 * Honest bound: the in-flight slot is released only when the worker actually
 * returns. There is no whole-request cancellation, so a permanently stuck
 * exchange holds its slot for the rest of the process - later sends drop
 * instead of queueing. That is the deliberate posture.
 */
object SendBudget {
    internal var maxAttemptsPerProcess=20
    internal var minIntervalMs=10_000L
    internal var rateLimitCooldownMs=60_000L
    private val attempts=AtomicInteger(0)
    private val lastAttemptAtMs=AtomicLong(0)
    private val cooldownUntilMs=AtomicLong(0)
    private val inFlight=AtomicBoolean(false)
    private val idleLock=Object()
    private const val DEFAULT_RESET_DRAIN_TIMEOUT_MS=15_000L
    // Test-tunable drain bound for resetForTesting; restored to the default on
    // every successful reset so cases stay isolated.
    internal var resetDrainTimeoutMs=DEFAULT_RESET_DRAIN_TIMEOUT_MS
    internal val attemptsSoFar:Int get()=attempts.get()
    fun tryAcquire():Boolean {
        // Single in-flight exchange; busy drops instead of queueing.
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
    /** Provider backoff: the longest server rate-limit window wins. */
    fun noteBackoffSeconds(seconds:Long) {
        if (seconds<=0L) return
        cooldownUntilMs.set(android.os.SystemClock.elapsedRealtime()+seconds*1000L)
    }
    fun release() {
        inFlight.set(false)
        synchronized(idleLock) { idleLock.notifyAll() }
    }
    /** Blocks the caller until no exchange is in flight or the deadline passes. */
    fun waitForIdle(timeoutMillis:Long) {
        val deadline=android.os.SystemClock.elapsedRealtime()+timeoutMillis
        synchronized(idleLock) {
            while (inFlight.get()) {
                val remaining=deadline-android.os.SystemClock.elapsedRealtime()
                if (remaining<=0L) return
                runCatching { idleLock.wait(minOf(remaining,100L)) }
            }
        }
    }
    /** Test-only: reset shared state and bounds so cases stay isolated. */
    fun resetForTesting() {
        // Drain BEFORE resetting: a previous case's exchange may still hold the
        // in-flight slot (its worker task can even be queued-but-unstarted).
        // Resetting under it would clear the flag beneath that exchange, and
        // its later release() would then clear a NEWER exchange's slot early,
        // so waitForIdle could return while that exchange is still running.
        waitForIdle(resetDrainTimeoutMs)
        // Fail closed on drain expiry: never reset beneath a live exchange -
        // that would reintroduce the very race the drain prevents. Throwing
        // fails the calling test loudly and leaves all state untouched.
        check(!inFlight.get()) {
            "SendBudget.resetForTesting: exchange still in flight after ${resetDrainTimeoutMs}ms drain; refusing to reset shared state beneath it"
        }
        resetDrainTimeoutMs=DEFAULT_RESET_DRAIN_TIMEOUT_MS
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
 * outcome here, and only after a completed exchange, so the debug diagnostic
 * reports only its own attempt.
 */
class WireAttemptResult {
    @Volatile var eventId:String?=null
    @Volatile var exchangeCompleted:Boolean=false
}

/**
 * The final transport, independent of every hook. Every envelope is rebuilt by
 * [EnvelopeGate] into the constants-only generic event and serialized to fixed
 * bytes BEFORE the worker handoff - the raw envelope and hint stay on the
 * caller, so attachments or hint carriers can never cross to the network path.
 * Only the byte array, the crash flush latch and the per-attempt result
 * reference cross, the latter two for lifecycle signaling only (never read,
 * never sent).
 *
 * A single bounded worker owns each exchange end to end: the send budget is
 * held across the actual blocking POST including cleanup (never merely an
 * enqueue), redirects are off, response rate-limit headers feed a cooldown,
 * and the response body is drained with a fixed byte cap. Client reports have
 * no attach point because the SDK async transport is not in the path; they are
 * additionally disabled at init.
 *
 * The original crash flush latch (DiskFlushNotification, the SDK's
 * uncaught-exception hint) is marked ONLY after a delivered exchange (2xx at
 * the HTTP layer - not a server-side storage receipt: a swallowed or truncated
 * response body still counts as accepted, matching the SDK's own transport
 * semantics). A failed or dropped send never marks the latch, so the uncaught
 * handler sees an honest failure instead of a false flush.
 *
 * Delivery is deliberately best-effort and bounded: a failure drops the report
 * instead of retrying, and init pairs this with the dedicated no-op envelope
 * cache, so crash reports never accumulate. There is no guaranteed crash
 * delivery. Connect and read timeouts bound those phases only: DNS resolution
 * and the request write use the JVM/socket defaults and there is no
 * whole-request deadline, so a stuck exchange holds its budget slot until the
 * worker actually returns (potentially for the process lifetime).
 */
class FilteringEnvelopeTransport(
    private val options:SentryOptions,
    private val release:String,
    private val environment:String,
    private val poster:(ByteArray)->Boolean = { bytes ->
        // No DSN configured means nothing to send; the report is dropped.
        options.dsn?.let { dsn -> defaultPost(options,dsn,bytes) } ?: false
    }
):ITransport {
    private val serializer by lazy { JsonSerializer(SentryOptions.empty()) }

    override fun send(envelope:SentryEnvelope,hint:Hint) {
        // The SDK's uncaught-exception integration attaches a flush latch and
        // blocks the crashing thread on waitFlush() after capture returns. The
        // typed SDK hint lives under the type-check wrapper; HintUtils recovers
        // it (plain io.sentry.Hint never implements DiskFlushNotification).
        val flushNotification=HintUtils.getSentrySdkHint(hint) as? DiskFlushNotification
        // Gate: only an envelope carrying an error/crash event item is worth
        // reporting; session, client-report, replay and attachment-only
        // envelopes are dropped and never converted into an error.
        if (envelope.items.none { it.header.type==SentryItemType.Event }) return
        // Sanitize BEFORE handoff: only the fixed generic event's bytes ever
        // reach the network path.
        val rebuilt=EnvelopeGate.rebuildToGenericEvent(envelope,release,environment) ?: return
        val wireEventId=rebuilt.header.eventId?.toString()
        val result=hint.getAs(RESULT_HINT_KEY,WireAttemptResult::class.java)
        val bytes=try {
            val out=ByteArrayOutputStream()
            serializer.serialize(rebuilt,out)
            out.toByteArray()
        } catch (_:Throwable) { return }
        // Local send budget: at most one exchange in flight, a minimum attempt
        // interval, a per-process cap and the server backoff window. A burst is
        // dropped, not queued.
        if (!SendBudget.tryAcquire()) return
        try {
            worker.execute {
                try {
                    val delivered=runCatching { poster(bytes) }.getOrDefault(false)
                    // Per-attempt correlation and the crash latch are completed
                    // ONLY after a delivered exchange; a failed or dropped send
                    // leaves both untouched.
                    if (delivered) {
                        result?.eventId=wireEventId
                        result?.exchangeCompleted=true
                        runCatching { flushNotification?.markFlushed() }
                    }
                } finally {
                    SendBudget.release()
                }
            }
        } catch (_:RejectedExecutionException) {
            // Worker gone (process shutting down): release the budget and drop.
            SendBudget.release()
        }
    }

    override fun isHealthy():Boolean=true
    // Rate limiting is enforced in SendBudget (minimum interval, per-process
    // cap and the server backoff window), so no SDK RateLimiter is exposed.
    override fun getRateLimiter():RateLimiter?=null
    /** Waits, up to [timeoutMillis], for any in-flight exchange to finish. */
    override fun flush(timeoutMillis:Long) { SendBudget.waitForIdle(timeoutMillis) }
    override fun close() { SendBudget.waitForIdle(IDLE_WAIT_TIMEOUT_MS) }
    override fun close(isRestarting:Boolean) { SendBudget.waitForIdle(IDLE_WAIT_TIMEOUT_MS) }

    companion object {
        internal const val RESULT_HINT_KEY="scrobblix.wireAttemptResult"
        private const val IDLE_WAIT_TIMEOUT_MS=10_000L
        private const val MAX_BACKOFF_SECONDS=3_600L
        private const val MAX_DRAIN_BYTES=8_192
        // One daemon worker for the process lifetime: the transport is
        // stateless per call, and permanently shutting a static executor would
        // kill later init/send cycles (close() drains only).
        private val worker=Executors.newSingleThreadExecutor { r ->
            Thread(r,"scrobblix-crash-sender").apply { isDaemon=true }
        }

        /**
         * Blocking POST of the final envelope bytes to the exact DSN endpoint:
         * HTTPS only (plain HTTP solely for loopback test endpoints, which
         * never leave the device), host/port taken verbatim from the DSN,
         * redirects disabled, connect/read deadlines, the endpoint path built
         * from the DSN and a code-owned auth header (SDK request headers are
         * not reused), with a fixed-length body so a buffered POST is never
         * transparently replayed after a connection loss. This transport never
         * installs a CookieHandler and sets no Cookie header; it cannot strip
         * cookies a process-global handler would add (none is installed by this
         * app). The connect/read timeouts bound those phases only: DNS
         * resolution and the request write use the JVM/socket defaults, and
         * there is no single whole-request deadline. Runs only on the worker
         * thread. Returns true only on a 2xx response (accepted at the HTTP
         * layer).
         */
        internal fun defaultPost(options:SentryOptions,dsn:String,bytes:ByteArray):Boolean {
            var conn:HttpURLConnection?=null
            try {
                val uri=URI(dsn)
                val host=uri.host ?: return false
                val key=uri.userInfo ?: return false
                val loopback=host=="127.0.0.1" || host=="::1" || host=="localhost"
                if (uri.scheme!="https" && !(loopback && uri.scheme=="http")) return false
                val port=if (uri.port>0) ":${uri.port}" else ""
                val endpoint="${uri.scheme}://$host$port/api${uri.path}/envelope/"
                conn=URI(endpoint).toURL().openConnection() as HttpURLConnection
                conn.instanceFollowRedirects=false
                conn.connectTimeout=options.connectionTimeoutMillis
                conn.readTimeout=options.readTimeoutMillis
                conn.requestMethod="POST"
                conn.doOutput=true
                // Fixed-length body: a buffered POST must never be transparently
                // replayed after a connection loss (one attempt, no retry).
                conn.setFixedLengthStreamingMode(bytes.size)
                // Fixed generic User-Agent: without an explicit value the
                // runtime sends its default (Dalvik version, OS release and
                // device build fingerprint), which is device metadata this
                // boundary must never emit. An explicit property replaces the
                // default outright; nothing is appended.
                conn.setRequestProperty("User-Agent","scrobblix-boundary/1")
                conn.setRequestProperty("Content-Type","application/x-sentry-envelope")
                conn.setRequestProperty(
                    "X-Sentry-Auth",
                    "Sentry sentry_version=7, sentry_client=scrobblix-boundary/1, sentry_key=$key"
                )
                conn.outputStream.use { it.write(bytes) }
                val code=conn.responseCode
                // Honor every rate-limit signal present on the response (max
                // wins, one-hour truncate, 429 floor), before touching the body.
                val backoffSeconds=computeBackoffSeconds(
                    conn.getHeaderField("X-Sentry-Rate-Limits"),
                    conn.getHeaderField("X-Sentry-Rate-Limit"),
                    conn.getHeaderField("Retry-After"),
                    System.currentTimeMillis()/1000L
                )
                when {
                    backoffSeconds!=null -> SendBudget.noteBackoffSeconds(backoffSeconds)
                    code==429 -> SendBudget.noteBackoffSeconds(SendBudget.rateLimitCooldownMs/1000L)
                }
                // Bounded drain: at most MAX_DRAIN_BYTES are read (bounded
                // memory and a bounded read count; each read is individually
                // bounded by the read timeout, so a slow trickle delays at most
                // reads x timeout - there is no whole-request deadline), then
                // the rest is cancelled via disconnect().
                val stream=runCatching { conn.inputStream }.getOrNull() ?: conn.errorStream
                stream?.let { discardBounded(it) }
                return code in 200..299
            } catch (_:Throwable) {
                // Reporting must never crash the app or retry: drop.
                return false
            } finally {
                try { conn?.disconnect() } catch (_:Throwable) {}
            }
        }

        /**
         * Longest server rate-limit window across every present signal:
         * X-Sentry-Rate-Limits entries (plural, comma-separated),
         * X-Sentry-Rate-Limit (legacy singular) and Retry-After (delta-seconds,
         * fractional or HTTP-date). Max wins, truncated at one hour BEFORE the
         * milliseconds conversion (no overflow). Fractional seconds round up.
         * Pure and test-visible.
         */
        internal fun computeBackoffSeconds(
            rateLimits:String?,
            rateLimit:String?,
            retryAfter:String?,
            nowEpochSeconds:Long
        ):Long? {
            val candidates=mutableListOf<Long>()
            rateLimits
                ?.split(',')
                ?.mapNotNull { parseSeconds(it.trim().substringBefore(':')) }
                ?.let { candidates.addAll(it) }
            rateLimit
                ?.let { parseSeconds(it.substringBefore(':').trim()) }
                ?.let { candidates.add(it) }
            retryAfter
                ?.let { header ->
                    parseSeconds(header.trim())
                        ?: runCatching {
                            ZonedDateTime.parse(header,DateTimeFormatter.RFC_1123_DATE_TIME).toEpochSecond()-nowEpochSeconds
                        }.getOrNull()
                }
                ?.let { candidates.add(it) }
            return candidates
                .filter { it>0L }
                .maxOrNull()
                ?.let { minOf(it,MAX_BACKOFF_SECONDS) }
        }

        private fun parseSeconds(raw:String):Long? =
            raw.toDoubleOrNull()?.takeIf { it>0.0 }?.let { kotlin.math.ceil(it).toLong() }

        private fun discardBounded(stream:InputStream) {
            runCatching {
                stream.use { s ->
                    val buffer=ByteArray(4096)
                    var remaining=MAX_DRAIN_BYTES
                    while (remaining>0) {
                        val n=s.read(buffer,0,minOf(buffer.size,remaining))
                        if (n<=0) break
                        remaining-=n
                    }
                }
            }
        }
    }
}
