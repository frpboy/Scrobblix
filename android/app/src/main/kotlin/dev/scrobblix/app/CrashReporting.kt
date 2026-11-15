package dev.scrobblix.app

import android.content.Context
import io.sentry.android.core.SentryAndroid
import io.sentry.transport.NoOpEnvelopeCache

/**
 * Optional automatic crash reporting. Disabled unless a DSN was baked in at build
 * time (SCROBBLIX_SENTRY_DSN); an empty DSN keeps diagnostics local-only.
 *
 * Privacy boundary, deliberate and minimal:
 * - uncaught exceptions only, via the SDK's default capture chain; no performance
 *   traces, no session replay, no screenshots or view hierarchies;
 * - no NDK/native crash capture, no scope sync, no ANR reporting: those carriers
 *   can include native tombstones, threads and scope data;
 * - no automatic breadcrumbs (user interaction, activity, network, lifecycle,
 *   system, component) and no automatic session tracking;
 * - sendDefaultPii stays false and no user/account is ever attached;
 * - the final transport boundary rebuilds every envelope from code-owned
 *   constants, so no exception, frame, thread or context content can leave the
 *   device regardless of what any producer attached;
 * - delivery is best-effort: no disk cache, queue depth one, failures drop.
 *   Crash reporting never blocks, retries indefinitely or crashes the app.
 */
object CrashReporting {
    private const val ENVIRONMENT="android"
    private const val RELEASE_PREFIX="scrobblix-android@"
    @Volatile private var enabled=false
    fun initialize(context:Context) {
        val dsn=BuildConfig.SCROBBLIX_SENTRY_DSN
        if (dsn.isEmpty()) return
        SentryAndroid.init(context) { options ->
            options.dsn=dsn
            options.isSendDefaultPii=false
            options.tracesSampleRate=0.0
            // Carriers that are off, explicitly: the SDK defaults several of these
            // to true, including NDK capture and scope sync.
            options.isEnableNdk=false
            options.isEnableScopeSync=false
            options.isAnrEnabled=false
            options.isEnableAutoSessionTracking=false
            options.isEnableUserInteractionBreadcrumbs=false
            options.isEnableActivityLifecycleBreadcrumbs=false
            options.isEnableNetworkEventBreadcrumbs=false
            options.isEnableAppLifecycleBreadcrumbs=false
            options.isEnableSystemEventBreadcrumbs=false
            options.isEnableAppComponentBreadcrumbs=false
            options.isAttachScreenshot=false
            options.isAttachViewHierarchy=false
            // Bounded, non-persistent delivery: no on-disk envelope cache and the
            // smallest send queue, so reports never pile up or survive the process.
            options.envelopeDiskCache=NoOpEnvelopeCache.getInstance()
            options.maxQueueSize=1
            options.environment=ENVIRONMENT
            options.release=RELEASE_PREFIX+BuildConfig.VERSION_NAME
            options.beforeSend=CrashEventFilter
            // Final gate at the transport boundary: whatever reaches this point is
            // rebuilt from code-owned constants under a fresh constant header.
            val release=RELEASE_PREFIX+BuildConfig.VERSION_NAME
            options.setTransportFactory { opts, requestDetails ->
                FilteringEnvelopeTransport(io.sentry.AsyncHttpTransportFactory().create(opts, requestDetails),release,ENVIRONMENT)
            }
        }
        enabled=true
    }
    fun capture(error:Throwable) { if (enabled) io.sentry.Sentry.captureException(error) }

    /**
     * Debug-only diagnostics affordance for ingest verification. Queues one
     * synthetic exception through the normal capture path; the transport gate
     * reduces it to the same generic constants-only event as a real crash, so
     * receipt is verified by timestamp correlation, never by echoed content.
     */
    fun captureDiagnosticTestReport():String {
        if (!BuildConfig.DEBUG) return "Test reports are only available in debug builds."
        if (!enabled) return "Crash reporting is not active in this build."
        capture(ScrobblixDiagnosticsTestException())
        return "Synthetic test report queued; it carries no listening data, account names or device content."
    }
}

class ScrobblixDiagnosticsTestException:Exception("synthetic diagnostics test report")
