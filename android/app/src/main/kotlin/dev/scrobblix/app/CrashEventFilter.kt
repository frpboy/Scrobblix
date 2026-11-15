package dev.scrobblix.app

import io.sentry.Hint
import io.sentry.SentryEvent
import io.sentry.SentryOptions

/**
 * The privacy boundary every outbound crash event passes through.
 *
 * - request, user, breadcrumbs, event message, tags and extras are dropped;
 * - exception MESSAGES are replaced with their type names because messages can
 *   embed track metadata, provider URLs or user paths (frames/mechanism stay);
 * - frame absolute paths are dropped (build/user home paths);
 * - contexts are reduced to an allowlist of generic build/device facts.
 * Attachments are never produced: the only SDK integrations that attach files
 * (screenshot, view hierarchy, thread dumps) are disabled at init.
 */
object CrashEventFilter:SentryOptions.BeforeSendCallback {
    private val allowedContexts=setOf("os","device","app","runtime")
    override fun execute(event:SentryEvent,hint:Hint):SentryEvent {
        event.request=null
        event.user=null
        event.breadcrumbs=null
        event.message=null
        event.tags?.clear()
        event.extra?.clear()
        event.exceptions?.forEach { exception ->
            exception.value=exception.type ?: "Error"
            exception.stacktrace?.frames?.forEach { it.absolutePath=null }
        }
        val contexts=event.contexts
        if (contexts!=null) {
            for (key in contexts.keys.toList()) if (key !in allowedContexts) contexts.remove(key)
        }
        return event
    }
}
