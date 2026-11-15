package dev.scrobblix.app

import android.content.Context
import io.sentry.android.core.SentryAndroid
import io.sentry.android.core.SentryAndroidOptions

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
 *   constants and serializes it before any worker handoff, so no exception,
 *   frame, thread, context or hint carrier can leave the device regardless of
 *   what any producer attached;
 * - client reports are off (init flag, and the SDK async transport that would
 *   attach them is not in the send path);
 * - delivery is best-effort and process-locally bounded: no disk persistence,
 *   at most one send in flight (held across the actual exchange, released only
 *   when the worker returns), a minimum attempt interval, a per-process
 *   attempt cap and a server backoff window; failures and budget excess drop.
 *   Crash reporting never crashes the app.
 */
object CrashReporting {
    private const val ENVIRONMENT="android"
    private const val RELEASE_PREFIX="scrobblix-android@"
    @Volatile private var enabled=false

    fun initialize(context:Context) {
        val dsn=BuildConfig.SCROBBLIX_SENTRY_DSN
        if (dsn.isEmpty() || enabled) return
        SentryAndroid.init(context) { options -> applyHardening(options,dsn) }
        enabled=true
    }

    /**
     * The complete production crash-reporting configuration, exactly as init
     * applies it. The persistence test drives the SDK init with this helper and
     * proves no cache or outbox writes happen after capture.
     */
    fun applyHardening(options:SentryAndroidOptions,dsn:String) {
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
        // Client reports off at init as well; the direct synchronous transport
        // is the authoritative boundary (the SDK async transport is the only
        // attach point and it is not in the path).
        options.isSendClientReports=false
        // Non-persistent delivery: a dedicated no-op envelope cache (NOT the SDK
        // sentinel, which the Android initializer would replace with the on-disk
        // AndroidEnvelopeCache) and no cache directory, so reports never pile up
        // or survive the process.
        options.setEnvelopeDiskCache(ScrobblixNoOpEnvelopeCache())
        options.cacheDirPath=null
        options.environment=ENVIRONMENT
        options.release=RELEASE_PREFIX+BuildConfig.VERSION_NAME
        options.beforeSend=CrashEventFilter
        // Final gate at the transport boundary: whatever reaches this point is
        // rebuilt from code-owned constants, serialized, and posted on a single
        // bounded worker.
        options.setTransportFactory { opts, _ ->
            FilteringEnvelopeTransport(opts,RELEASE_PREFIX+BuildConfig.VERSION_NAME,ENVIRONMENT)
        }
    }

    fun capture(error:Throwable) { if (enabled) io.sentry.Sentry.captureException(error) }

    /**
     * Debug-only diagnostics affordance for ingest verification. Queues one
     * synthetic exception through the normal capture path on a background
     * thread; the transport gate reduces it to the same generic constants-only
     * event as a real crash, and the wait is bounded by the latch timeout.
     * Receipt is verified remotely by the exact wire event id, never by echoed
     * content.
     */
    fun captureDiagnosticTestReport():String {
        if (!BuildConfig.DEBUG) return "Test reports are only available in debug builds."
        if (!enabled) return "Crash reporting is not active in this build."
        // Attempt-specific correlation carried on the hint: the result and the
        // crash latch belong to THIS attempt only. Call off the UI thread: the
        // wait is bounded by the latch timeout.
        val result=WireAttemptResult()
        val latch=io.sentry.UncaughtExceptionHandlerIntegration.UncaughtExceptionHint(20_000,io.sentry.NoOpLogger.getInstance())
        val hint=io.sentry.util.HintUtils.createWithTypeCheckHint(latch)
        hint.set(FilteringEnvelopeTransport.RESULT_HINT_KEY,result)
        io.sentry.Sentry.captureException(ScrobblixDiagnosticsTestException(),hint)
        latch.waitFlush()
        val wireId=result.eventId
        // Deliberately "attempted", never "sent": only a remote receipt matching
        // this exact wire event id counts as received.
        return if (result.exchangeCompleted && wireId!=null) "Attempted; count as received only when remote receipt matches event id $wireId. Carries no listening data, account names or device content."
        else "Attempted; no completed exchange (send budget, rate limit or downstream drop). Nothing was persisted."
    }
}

class ScrobblixDiagnosticsTestException:Exception("synthetic diagnostics test report")
