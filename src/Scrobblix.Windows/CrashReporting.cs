using Sentry;

namespace Scrobblix.Windows;

/// <summary>
/// Optional automatic crash reporting. Disabled unless a DSN was baked in at
/// build time through the SCROBBLIX_SENTRY_DSN environment variable, which the
/// build bakes into generated source (ScrobblixSentryDsn.g.cs); an empty DSN
/// keeps diagnostics local-only, matching the documented baseline.
///
/// Privacy boundary, deliberate and enforced at the transport gate:
/// - the outbound envelope is REBUILT from code-owned constants with a fresh
///   event id and timestamp; nothing derived from the captured exception, the
///   device or the SDK pipeline crosses the wire: no exception type names or
///   values (type names are input-derived too), no frames/threads/mechanism/
///   contexts/tags/extras/breadcrumbs/request/user, no original or SDK headers;
/// - the event says only: this app version on this platform reported an
///   unhandled error;
/// - <see cref="ApplyPrivacyBoundary"/> remains as defense-in-depth on the
///   hook path, but the gate is the authoritative boundary;
/// - unhandled exceptions only: no performance traces, no session replay,
///   no screenshots; sendDefaultPii stays false and no user/account is ever
///   attached; listening history, queue contents, artwork paths and
///   credentials are never attached to events;
/// - capture is the SDK default chain (AppDomain unhandled exceptions and
///   unobserved task exceptions); client reports are off; delivery is
///   best-effort, process-locally bounded (queue depth one, one send in
///   flight, minimum attempt interval, per-process attempt cap; bounds reset
///   on restart) and never persisted to disk.
/// </summary>
public static class CrashReporting
{
    // Generated at build time from SCROBBLIX_SENTRY_DSN (ScrobblixSentryDsn.g.cs).
    private const string Dsn = ScrobblixSentryDsn.Value;

    private static bool _enabled;

    public static void Initialize()
    {
        if (Dsn.Length == 0 || _enabled) return;
        var release = "scrobblix-windows@" + (
            typeof(CrashReporting).Assembly
                .GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
                is { Length: > 0 } attrs
                ? ((System.Reflection.AssemblyInformationalVersionAttribute)attrs[0]).InformationalVersion
                : typeof(CrashReporting).Assembly.GetName().Version?.ToString() ?? "unknown");
        SentrySdk.Init(options =>
        {
            options.Dsn = Dsn;
            options.Release = release;
            options.Environment = "windows";
            options.SendDefaultPii = false;
            options.TracesSampleRate = 0.0;
            options.AutoSessionTracking = false;
            options.SendClientReports = false;
            options.MaxQueueItems = 1;
            options.IsGlobalModeEnabled = true;
            options.StackTraceMode = StackTraceMode.Original;
            options.SetBeforeSend((sentryEvent, hint) => ApplyPrivacyBoundary(sentryEvent, hint));
            // Final gate at the transport boundary: whatever reaches this point is
            // rebuilt into a constants-only generic event. Redirects stay off so a
            // hostile or misconfigured endpoint can never bounce the payload.
            options.Transport = new FilteringEnvelopeTransport(options, new HttpClient(new HttpClientHandler
            {
                AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate,
                AllowAutoRedirect = false
            }), release, "windows");
        });
        // Capture is the SDK default integration chain (AppDomain unhandled
        // exceptions + unobserved task exceptions). No custom duplicate handlers.
        _enabled = true;
    }

    /// <summary>The privacy boundary on the hook path, defense-in-depth only.</summary>
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
                exception.Value = FilteringEnvelopeTransport.GenericType;
                exception.Type = FilteringEnvelopeTransport.GenericType;
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
            sentryEvent.Contexts.Remove(key);
        }
        return sentryEvent;
    }

    public static void Capture(Exception error)
    {
        if (_enabled) SentrySdk.CaptureException(error);
    }

    /// <summary>
    /// Deliberate synthetic crash report for validating the ingest route end to
    /// end, gated to debug builds. It goes through the same boundary and is
    /// never disguised as a real crash. The generic gate rebuilds it to the same
    /// constants-only event as a real crash, so it is identified solely by the
    /// wire event id shown after the attempt. Safe by construction: the gate
    /// leaves no real exception content behind, so no real crash has to be
    /// induced and no user data is at risk. Note this exercises the capture
    /// path in a debug build only; it is not signed-release or unhandled-path
    /// proof, which remains a separately held ingest test.
    /// </summary>
    public static string CaptureDiagnosticTestReport()
    {
#if !DEBUG
        return "disabled (non-debug build)";
#else
        if (!_enabled) return "disabled (no DSN configured in this build)";
        try
        {
            FilteringEnvelopeTransport.ResetLastWireEventId();
            SentrySdk.CaptureException(new ScrobblixDiagnosticsTestException());
            SentrySdk.Flush(TimeSpan.FromSeconds(5));
            // Deliberately "attempted", never "sent": a local flush proves nothing
            // about remote receipt. Count it received only when the remote project
            // shows this exact wire event id.
            var wireId = FilteringEnvelopeTransport.LastWireEventId;
            return wireId is not null
                ? "attempted; count as received only when remote receipt matches event id " + wireId
                : "attempted; no wire event id recorded (send budget or queue). Nothing was persisted.";
        }
        catch (Exception error)
        {
            return "attempt failed locally: " + error.GetType().Name;
        }
#endif
    }
}

/// <summary>Exception type used only by <see cref="CrashReporting.CaptureDiagnosticTestReport"/>.</summary>
public sealed class ScrobblixDiagnosticsTestException : Exception
{
    public ScrobblixDiagnosticsTestException() : base("Synthetic crash-reporting test. This is not a real crash.") { }
}
