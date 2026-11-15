package dev.scrobblix.app

import io.sentry.JsonSerializer
import io.sentry.SentryEnvelope
import io.sentry.SentryEnvelopeHeader
import io.sentry.SentryEnvelopeItem
import io.sentry.SentryEvent
import io.sentry.SentryItemType
import io.sentry.SentryLevel
import io.sentry.SentryOptions
import io.sentry.protocol.SentryException

/**
 * Final outbound boundary, independent of any hook. Every envelope that reaches
 * this point is either dropped or rebuilt from code-owned constants with a fresh
 * event id and timestamp. Nothing derived from the captured exception, the
 * device or the SDK pipeline crosses this boundary: original and SDK headers,
 * exception type names and values (type names are input-derived too), frames,
 * threads, mechanism, contexts, tags, extras, breadcrumbs, request, user.
 *
 * The outbound event intentionally says only: this app version on this platform
 * reported an unhandled error. Receipt verification for the ingest test is by
 * timestamp correlation, never by echoed content.
 */
object EnvelopeGate {
    const val GENERIC_TYPE="ScrobblixGenericCrash"
    private val serializer by lazy { JsonSerializer(SentryOptions.empty()) }

    fun rebuildToGenericEvent(envelope:SentryEnvelope, release:String, environment:String):SentryEnvelope? {
        val hasEvent=envelope.items.any { it.header.type==SentryItemType.Event }
        if (!hasEvent) return null
        val event=SentryEvent()
        event.level=SentryLevel.ERROR
        event.logger="scrobblix.crash"
        event.release=release
        event.environment=environment
        event.setExceptions(listOf(SentryException().apply { type=GENERIC_TYPE; value=GENERIC_TYPE }))
        val item=SentryEnvelopeItem.fromEvent(serializer,event)
        // Fresh constant header: new event id only. No trace context, no SDK header,
        // no unknown keys - whatever the original envelope carried stays behind.
        val header=SentryEnvelopeHeader(event.eventId,null,null)
        return SentryEnvelope(header,listOf(item))
    }
}
