package dev.scrobblix.app

import io.sentry.Hint
import io.sentry.SentryEnvelope
import io.sentry.transport.ITransport
import io.sentry.transport.RateLimiter

/**
 * Wraps the real HTTP transport so the final wire shape is enforced by
 * [EnvelopeGate] no matter what any hook or producer emitted.
 *
 * Delivery is deliberately best-effort and bounded: a failure or a busy pipe
 * drops the report instead of retrying, and init pairs this with a no-op disk
 * cache and a queue depth of one, so crash reports never accumulate. There is
 * no guaranteed crash delivery; that is the documented trade for a minimal
 * network and storage budget.
 */
class FilteringEnvelopeTransport(
    private val delegate:ITransport,
    private val release:String,
    private val environment:String
):ITransport {
    override fun send(envelope:SentryEnvelope,hint:Hint) {
        val rebuilt=EnvelopeGate.rebuildToGenericEvent(envelope,release,environment) ?: return
        try {
            delegate.send(rebuilt,hint)
        } catch (_:Throwable) {
            // Deliberate drop: crash reporting must never crash or block the app.
        }
    }
    override fun flush(timeoutMillis:Long)=delegate.flush(timeoutMillis)
    override fun isHealthy():Boolean=delegate.isHealthy()
    override fun getRateLimiter():RateLimiter?=delegate.getRateLimiter()
    override fun close()=delegate.close()
    override fun close(isRestarting:Boolean)=delegate.close(isRestarting)
}
