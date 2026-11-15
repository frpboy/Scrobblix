package dev.scrobblix.app

import io.sentry.Hint
import io.sentry.SentryEnvelope
import io.sentry.transport.ITransport
import io.sentry.transport.RateLimiter

/**
 * Wraps the real HTTP transport so the final wire shape is enforced by
 * [EnvelopeGate] no matter what any hook or producer emitted.
 */
class FilteringEnvelopeTransport(private val delegate:ITransport):ITransport {
    override fun send(envelope:SentryEnvelope,hint:Hint) {
        val rebuilt=EnvelopeGate.rebuildToEventsOnly(envelope) ?: return
        delegate.send(rebuilt,hint)
    }
    override fun flush(timeoutMillis:Long)=delegate.flush(timeoutMillis)
    override fun isHealthy():Boolean=delegate.isHealthy()
    override fun getRateLimiter():RateLimiter?=delegate.getRateLimiter()
    override fun close()=delegate.close()
    override fun close(isRestarting:Boolean)=delegate.close(isRestarting)
}
