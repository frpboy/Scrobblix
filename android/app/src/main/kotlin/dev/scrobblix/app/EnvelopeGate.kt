package dev.scrobblix.app

import io.sentry.SentryEnvelope
import io.sentry.SentryEnvelopeHeader
import io.sentry.SentryItemType

/**
 * The final transport gate, independent of every hook: whatever envelope reaches
 * the wire is rebuilt to event items only, under a fresh constant header
 * (event id + SDK version; no trace context, nothing else). Non-event envelopes
 * (sessions, client reports, replays) are dropped entirely.
 */
object EnvelopeGate {
    fun rebuildToEventsOnly(envelope:SentryEnvelope):SentryEnvelope? {
        val eventItems=envelope.items.filter { it.header.type==SentryItemType.Event }
        if (eventItems.isEmpty()) return null
        return SentryEnvelope(SentryEnvelopeHeader(envelope.header.eventId, envelope.header.sdkVersion), eventItems)
    }
}
