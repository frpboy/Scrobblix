using Sentry;

namespace Scrobblix.Windows;

/// <summary>
/// Optional automatic crash reporting. Disabled unless a DSN was baked in at build
/// time (SCROBBLIX_SENTRY_DSN); an empty DSN keeps diagnostics local-only, matching
/// the documented baseline.
///
/// Privacy boundary, deliberate and enforced in <see cref="ApplyPrivacyBoundary"/>:
/// - unhandled exceptions only: no performance traces, no session replay,
///   no screenshots, no breadcrumbs, no request/user/extra payloads;
/// - exception MESSAGES are replaced with their type names because messages can
///   embed track metadata, URLs or user paths; stack frames and mechanism stay;
/// - sendDefaultPii stays false and no user/account is ever attached;
/// - listening history, queue contents, artwork paths and credentials are never
///   attached to events.
/// </summary>
public static class CrashReporting
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
            options.Environment = "windows";
            options.SetBeforeSend((sentryEvent, _) => ApplyPrivacyBoundary(sentryEvent));
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

    private static readonly HashSet<string> AllowedContexts = new(StringComparer.Ordinal) { "os", "device", "app", "runtime" };

    /// <summary>The privacy boundary every outbound event passes through.</summary>
    public static SentryEvent ApplyPrivacyBoundary(SentryEvent sentryEvent)
    {
        sentryEvent.Request = null;
        sentryEvent.User = null;
        sentryEvent.ClearBreadcrumbs();
        sentryEvent.Message = null;
        sentryEvent.Tags.Clear();
        sentryEvent.SetExtras(Array.Empty<KeyValuePair<string, object?>>());
        if (sentryEvent.SentryExceptions is not null)
        {
            foreach (var exception in sentryEvent.SentryExceptions)
            {
                exception.Value = exception.Type ?? "Error";
                if (exception.Stacktrace?.Frames is not null)
                {
                    foreach (var frame in exception.Stacktrace.Frames)
                    {
                        frame.AbsolutePath = null;
                    }
                }
            }
        }
        foreach (var key in sentryEvent.Contexts.Keys.ToList())
        {
            if (!AllowedContexts.Contains(key)) sentryEvent.Contexts.Remove(key);
        }
        return sentryEvent;
    }

    public static void Capture(Exception error)
    {
        if (_enabled) SentrySdk.CaptureException(error);
    }
}
