package dev.scrobblix.app

import android.content.Context
import io.sentry.android.core.SentryAndroid

/**
 * Optional automatic crash reporting. Disabled unless a DSN was baked in at build
 * time (SCROBBLIX_SENTRY_DSN); an empty DSN keeps diagnostics local-only.
 *
 * Privacy boundary, deliberate and minimal:
 * - uncaught exceptions and their stack traces only; no performance traces,
 *   no session replay, no screenshots or view hierarchies;
 * - no automatic breadcrumbs (user interaction, network, lifecycle, system,
 *   component): Last.fm request URLs and UI text must never leave the device;
 * - sendDefaultPii stays false and no user/account is ever attached;
 * - listening history, track metadata, queue contents and credentials are never
 *   attached to events.
 */
object CrashReporting {
    @Volatile private var enabled=false
    fun initialize(context:Context) {
        val dsn=BuildConfig.SCROBBLIX_SENTRY_DSN
        if (dsn.isEmpty()) return
        SentryAndroid.init(context) { options ->
            options.dsn=dsn
            options.isSendDefaultPii=false
            options.tracesSampleRate=0.0
            options.isEnableUserInteractionBreadcrumbs=false
            options.isEnableNetworkEventBreadcrumbs=false
            options.isEnableAppLifecycleBreadcrumbs=false
            options.isEnableSystemEventBreadcrumbs=false
            options.isEnableAppComponentBreadcrumbs=false
            options.isAttachScreenshot=false
            options.isAttachViewHierarchy=false
        }
        enabled=true
    }
    fun capture(error:Throwable) { if (enabled) io.sentry.Sentry.captureException(error) }
}
