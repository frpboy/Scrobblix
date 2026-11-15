using Sentry;

namespace Scrobblix.Desktop;

/// <summary>
/// Optional automatic crash reporting. Disabled unless a DSN was baked in at build
/// time (SCROBBLIX_SENTRY_DSN); an empty DSN keeps diagnostics local-only, matching
/// the documented baseline.
///
/// Privacy boundary, deliberate and minimal:
/// - unhandled exceptions and their stack traces only; no performance traces,
///   no session replay, no screenshots;
/// - sendDefaultPii stays false and no user/account is ever attached;
/// - listening history, track metadata, queue contents, artwork paths and
///   credentials are never attached to events.
/// </summary>
internal static class CrashReporting
{
#if SCROBBLIX_SENTRY_DSN
    private const string Dsn = SCROBBLIX_SENTRY_DSN;
#else
    private const string Dsn = "";
#endif

    private static bool _enabled;

    public static void Initialize()
    {
        if (Dsn.Length == 0 || _enabled) return;
        SentrySdk.Init(options =>
        {
            options.Dsn = Dsn;
            options.SendDefaultPii = false;
            options.TracesSampleRate = 0.0;
            options.AutoSessionTracking = false;
            options.IsGlobalModeEnabled = true;
            options.StackTraceMode = StackTraceMode.Original;
            // We capture WinUI UI-thread exceptions explicitly through
            // Application.UnhandledException; no implicit integrations add payloads.
            options.DisableWinUiUnhandledExceptionIntegration();
        });
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            Capture(args.ExceptionObject as Exception ?? new InvalidOperationException("Unknown process exception"));
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            Capture(args.Exception);
            args.SetObserved();
        };
        _enabled = true;
    }

    public static void Capture(Exception error)
    {
        if (_enabled) SentrySdk.CaptureException(error);
    }
}
