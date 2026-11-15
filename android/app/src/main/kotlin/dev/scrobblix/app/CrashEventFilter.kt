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
        event.tags=null
        event.extras=null
        event.exceptions?.forEach { exception ->
            exception.value=exception.type ?: "Error"
            exception.stacktrace?.frames?.forEach { it.absPath=null }
        }
        val contexts=event.contexts
        // Contexts is not a Map: keys() returns an Enumeration over internal storage.
        for (key in java.util.Collections.list(contexts.keys())) {
            if (key !in allowedContexts) contexts.remove(key)
        }
        return event
    }
}
