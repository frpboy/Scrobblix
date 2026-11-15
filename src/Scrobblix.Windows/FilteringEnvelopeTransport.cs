using System.Collections.Concurrent;
using Sentry;
using Sentry.Extensibility;
using Sentry.Http;
using Sentry.Protocol.Envelopes;

namespace Scrobblix.Windows;

/// <summary>
/// The final transport gate, independent of every hook. Every envelope that
/// reaches this point is either dropped or rebuilt from code-owned constants
/// with a fresh event id and timestamp. Nothing derived from the captured
/// exception, the device or the SDK pipeline crosses this boundary: original
/// and SDK headers, exception type names and values (type names are
/// input-derived too), frames, threads, mechanism, contexts, tags, extras,
/// breadcrumbs, request, user.
///
/// The outbound event intentionally says only: this app version on this
/// platform reported an unhandled error. Receipt verification for the ingest
/// test is by timestamp correlation, never by echoed content.
///
/// Delivery is deliberately best-effort and bounded: a send failure drops the
/// report instead of retrying, no cache directory is configured, and a shared
/// send budget caps attempts (one in flight, minimum interval, per-process cap),
/// so crash reports never accumulate and a crash loop never becomes a network
/// loop. There is no guaranteed crash delivery.
/// Mirrors Sentry 7.0.0's internal HttpTransport send flow over the public
/// HttpTransportBase.
/// </summary>
public sealed class FilteringEnvelopeTransport : HttpTransportBase, ITransport
{
    public const string GenericType = "ScrobblixGenericCrash";

    // Process-local hygiene bounds (they reset on restart): within a process a
    // crash loop cannot become a network or quota loop. Static and settable so
    // tests can inject deterministic budgets.
    public static int MaxAttemptsPerProcess { get; set; } = 20;
    public static TimeSpan MinAttemptInterval { get; set; } = TimeSpan.FromSeconds(10);

    private static int _attempts;
    private static long _lastAttemptTicks;
    private static int _inFlight;

    /// <summary>Test-only: reset shared budget state and bounds.</summary>
    public static void ResetBudgetForTesting()
    {
        Interlocked.Exchange(ref _attempts, 0);
        Interlocked.Exchange(ref _lastAttemptTicks, 0);
        Interlocked.Exchange(ref _inFlight, 0);
        MaxAttemptsPerProcess = 20;
        MinAttemptInterval = TimeSpan.FromSeconds(10);
    }

    private readonly HttpClient _httpClient;
    private readonly string _release;
    private readonly string _environment;

    /// <summary>Per-attempt correlation result: one immutable entry per
    /// envelope that passed the gate, keyed by the ORIGINAL envelope event id
    /// (a random SDK-generated id, used in-process only and never sent). A
    /// completed exchange publishes a new completed entry atomically, so the
    /// wire id and completion flag can never be observed inconsistently;
    /// concurrent attempts never overwrite each other's result.</summary>
    public sealed class AttemptResult
    {
        public AttemptResult(string? eventId, bool exchangeCompleted, string? wireTimestamp)
        {
            EventId = eventId;
            ExchangeCompleted = exchangeCompleted;
            WireTimestamp = wireTimestamp;
        }
        public string? EventId { get; }
        public bool ExchangeCompleted { get; }
        // DISPOSABLE-branch readback (test lane only): the exact timestamp the
        // gate put on the FINAL wire event, teed with the id for the project
        // owner's exact receipt lookup.
        public string? WireTimestamp { get; }
    }

    private static readonly ConcurrentDictionary<string, AttemptResult> Attempts = new();

    /// <summary>The per-attempt result for a captured original event id, or
    /// null when the attempt never reached the wire path (dropped at the gate
    /// or by the send budget).</summary>
    public static AttemptResult? GetAttemptResult(string originalEventId) =>
        Attempts.TryGetValue(originalEventId, out var result) ? result : null;

    public FilteringEnvelopeTransport(SentryOptions options, HttpClient httpClient, string release, string environment) : base(options)
    {
        _httpClient = httpClient;
        _release = release;
        _environment = environment;
    }

    public async Task SendEnvelopeAsync(Envelope envelope, CancellationToken cancellationToken = default)
    {
        var rebuilt = RebuildToGenericEvent(envelope, _release, _environment, out var wireTimestamp);
        if (rebuilt is null) return;
        // Bounded send budget: one send in flight, a minimum attempt interval and a
        // hard per-process cap, so a crash loop can never become a network or quota
        // loop. Exceeding the budget drops the report.
        if (Interlocked.Exchange(ref _inFlight, 1) == 1) return;
        try
        {
            // Per-attempt correlation keyed by the original (never-sent) event id.
            string? attemptKey = null;
            if (envelope.Header.TryGetValue("event_id", out var originalId) && originalId is not null)
            {
                // Bounded diagnostic bookkeeping, not persistence: keep the table small.
                if (Attempts.Count > 64) Attempts.Clear();
                attemptKey = originalId.ToString()!;
                Attempts[attemptKey] = new AttemptResult(null, false, null);
            }
            var nowTicks = DateTimeOffset.UtcNow.UtcTicks;
            if (Volatile.Read(ref _attempts) >= MaxAttemptsPerProcess ||
                nowTicks - Interlocked.Read(ref _lastAttemptTicks) < MinAttemptInterval.Ticks) return;
            Interlocked.Increment(ref _attempts);
            Interlocked.Exchange(ref _lastAttemptTicks, nowTicks);
            // SendClientReports is disabled at init, so ProcessEnvelope attaches
            // nothing after this rebuild: the generic event is the whole wire body.
            var processedEnvelope = ProcessEnvelope(rebuilt);
            if (processedEnvelope.Items.Count > 0)
            {
                try
                {
                    using var request = CreateRequest(processedEnvelope);
                    using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
                    await HandleResponseAsync(response, processedEnvelope, cancellationToken).ConfigureAwait(false);
                    // Only a completed exchange earns the rebuilt wire event id,
                    // published atomically; a dropped or failed attempt leaves the
                    // pending entry untouched.
                    if (response.IsSuccessStatusCode && attemptKey is not null)
                    {
                        var wireId = rebuilt.Header.TryGetValue("event_id", out var id) ? id?.ToString() : null;
                        Attempts[attemptKey] = new AttemptResult(wireId, true, wireTimestamp.ToString("o", System.Globalization.CultureInfo.InvariantCulture));
                    }
                }
                catch (Exception)
                {
                    // Deliberate drop: crash reporting must never crash or block the app.
                }
            }
        }
        finally
        {
            Interlocked.Exchange(ref _inFlight, 0);
        }
    }

    public static Envelope? RebuildToGenericEvent(Envelope envelope, string release, string environment)
        => RebuildToGenericEvent(envelope, release, environment, out _);

    // DISPOSABLE-branch overload (test lane only): also returns the exact FINAL
    // wire timestamp the gate put on the rebuilt event, so the synthetic test
    // tees UUID+timestamp for the project owner's exact receipt lookup.
    public static Envelope? RebuildToGenericEvent(Envelope envelope, string release, string environment, out DateTimeOffset wireTimestamp)
    {
        if (!envelope.Items.Any(item => item.TryGetType() == "event"))
        {
            wireTimestamp = default;
            return null;
        }
        var fresh = new SentryEvent
        {
            Level = SentryLevel.Error,
            Logger = "scrobblix.crash",
            Release = release,
            Environment = environment,
            SentryExceptions = new[] { new Sentry.Protocol.SentryException { Type = GenericType, Value = GenericType } }
        };
        // Fresh constant header: the new event id only. No trace context, no SDK
        // header, no unknown keys - whatever the original envelope carried stays
        // behind.
        wireTimestamp = fresh.Timestamp;
        var header = new Dictionary<string, object?> { ["event_id"] = fresh.EventId.ToString() };
        return new Envelope(header, new[] { EnvelopeItem.FromEvent(fresh) });
    }
}
