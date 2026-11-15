package dev.scrobblix.app

import io.sentry.Hint
import io.sentry.SentryEnvelope
import io.sentry.cache.IEnvelopeCache
import io.sentry.transport.ITransport
import io.sentry.transport.RateLimiter
import java.util.concurrent.atomic.AtomicBoolean
import java.util.concurrent.atomic.AtomicInteger
import java.util.concurrent.atomic.AtomicLong

/**
 * Dedicated no-op envelope cache. This is deliberately NOT the SDK's
 * NoOpEnvelopeCache sentinel: the Android initializer replaces the sentinel with
 * AndroidEnvelopeCache whenever a cache path exists, so only a dedicated class
 * guarantees nothing is ever persisted. Crash reports are never written to
 * disk and never replayed.
 */
class ScrobblixNoOpEnvelopeCache:IEnvelopeCache {
    override fun store(envelope:SentryEnvelope,hint:Hint) {}
    override fun discard(envelope:SentryEnvelope) {}
    override fun iterator():MutableIterator<SentryEnvelope> = mutableListOf<SentryEnvelope>().iterator()
}

/**
 * Bounded send budget shared by every transport instance: at most one send in
 * flight, a minimum interval between attempts, and a hard per-process attempt
 * cap. A crash loop can therefore never turn into a network or quota loop.
 * Exceeding the budget drops the report; there is no guaranteed delivery.
 */
object SendBudget {
    private const val MAX_ATTEMPTS_PER_PROCESS=20
    private const val MIN_INTERVAL_MS=10_000L
    private val attempts=AtomicInteger(0)
    private val lastAttemptAtMs=AtomicLong(0)
    private val inFlight=AtomicBoolean(false)
    fun tryAcquire():Boolean {
        if (!inFlight.compareAndSet(false,true)) return false
        val now=android.os.SystemClock.elapsedRealtime()
        if (attempts.get()>=MAX_ATTEMPTS_PER_PROCESS || now-lastAttemptAtMs.get()<MIN_INTERVAL_MS) {
            inFlight.set(false)
            return false
        }
        attempts.incrementAndGet()
        lastAttemptAtMs.set(now)
        return true
    }
    fun release() { inFlight.set(false) }
    /** Test-only: reset the shared budget so instrumented cases stay isolated. */
    fun resetForTesting() {
        attempts.set(0)
        lastAttemptAtMs.set(0)
        inFlight.set(false)
    }
}

/**
 * Wraps the real HTTP transport so the final wire shape is enforced by
 * [EnvelopeGate] no matter what any hook or producer emitted.
 *
 * The downstream hint is always a FRESH Hint: the SDK only attaches client
 * reports to envelopes carrying an UncaughtExceptionHint marker, and the fresh
 * hint removes that marker, so nothing but the rebuilt generic event can reach
 * the connection even on the uncaught path. Client reports are additionally
 * disabled at init (isSendClientReports=false).
 *
 * Delivery is deliberately best-effort and bounded: [SendBudget] caps attempts,
 * a failure drops the report instead of retrying, and init pairs this with the
 * dedicated no-op envelope cache and a queue depth of one, so crash reports
 * never accumulate. There is no guaranteed crash delivery; that is the
 * documented trade for a minimal network and storage budget.
 */
class FilteringEnvelopeTransport(
    private val delegate:ITransport,
    private val release:String,
    private val environment:String
):ITransport {
    override fun send(envelope:SentryEnvelope,hint:Hint) {
        val rebuilt=EnvelopeGate.rebuildToGenericEvent(envelope,release,environment) ?: return
        if (!SendBudget.tryAcquire()) return
        try {
            lastWireEventId=rebuilt.header.eventId?.toString()
            delegate.send(rebuilt,Hint())
        } catch (_:Throwable) {
            // Deliberate drop: crash reporting must never crash or block the app.
        } finally {
            SendBudget.release()
        }
    }
    override fun flush(timeoutMillis:Long)=delegate.flush(timeoutMillis)
    override fun isHealthy():Boolean=delegate.isHealthy()
    override fun getRateLimiter():RateLimiter?=delegate.getRateLimiter()
    override fun close()=delegate.close()
    override fun close(isRestarting:Boolean)=delegate.close(isRestarting)
    companion object {
        /** Event id of the most recent rebuilt event handed to the wire. Receipt
         *  verification matches this id remotely; it is never a delivery claim. */
        @Volatile var lastWireEventId:String?=null
            private set
    }
}
