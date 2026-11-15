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
            options.SetBeforeSend((sentryEvent, hint) => ApplyPrivacyBoundary(sentryEvent, hint));
            // Final gate at the transport boundary: whatever reaches this point is
            // rebuilt to an event-only envelope with a fresh constant header.
            options.Transport = new FilteringEnvelopeTransport(options, new HttpClient(new HttpClientHandler
            {
                AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate
            }));
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
    public static SentryEvent ApplyPrivacyBoundary(SentryEvent sentryEvent, SentryHint? hint = null)
    {
        // Attachments bypass event field mutation: they travel on the hint (scope
        // attachments are merged into it before this callback runs). Drop them all.
        hint?.Attachments.Clear();
        // Sentry 7.0.0: Request/User setters are non-nullable, but the backing fields
        // are nullable and serialization skips them when null.
        sentryEvent.Request = null!;
        sentryEvent.User = null!;
        // Sentry 7.0.0 exposes Breadcrumbs/Tags as read-only views over the backing
        // List/Dictionary with no public clear API; the cast is stable for this pinned
        // SDK version and empties the carrier the serializer would otherwise write.
        ((List<Breadcrumb>)sentryEvent.Breadcrumbs).Clear();
        sentryEvent.Message = null;
        ((Dictionary<string, string>)sentryEvent.Tags).Clear();
        // SetExtras MERGES into the backing dictionary (IHasExtra.cs loops SetExtra),
        // so clearing needs the same backing-store cast as tags/breadcrumbs.
        ((Dictionary<string, object?>)sentryEvent.Extra).Clear();
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
