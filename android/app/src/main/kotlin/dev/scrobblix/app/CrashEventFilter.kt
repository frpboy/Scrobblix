package dev.scrobblix.app

import io.sentry.Hint
import io.sentry.SentryEvent
import io.sentry.SentryOptions

/**
 * The privacy boundary every outbound crash event passes through.
 *
 * Exception MESSAGES are replaced with their type names because messages can embed
 * track metadata, provider URLs or user paths; stack frames and mechanism stay.
 * Request, user, breadcrumbs and event message are dropped entirely.
 */
object CrashEventFilter:SentryOptions.BeforeSendCallback {
    override fun execute(event:SentryEvent,hint:Hint):SentryEvent {
        event.request=null
        event.user=null
        event.breadcrumbs=null
        event.message=null
        event.exceptions?.forEach { it.value=it.type ?: "Error" }
        return event
    }
}
