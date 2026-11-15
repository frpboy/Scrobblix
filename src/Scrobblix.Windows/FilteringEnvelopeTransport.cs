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
/// report instead of retrying, and no cache directory is configured, so crash
/// reports never accumulate on disk. There is no guaranteed crash delivery.
/// Mirrors Sentry 7.0.0's internal HttpTransport send flow over the public
/// HttpTransportBase.
/// </summary>
public sealed class FilteringEnvelopeTransport : HttpTransportBase, ITransport
{
    public const string GenericType = "ScrobblixGenericCrash";

    private readonly HttpClient _httpClient;
    private readonly string _release;
    private readonly string _environment;

    public FilteringEnvelopeTransport(SentryOptions options, HttpClient httpClient, string release, string environment) : base(options)
    {
        _httpClient = httpClient;
        _release = release;
        _environment = environment;
    }

    public async Task SendEnvelopeAsync(Envelope envelope, CancellationToken cancellationToken = default)
    {
        var rebuilt = RebuildToGenericEvent(envelope, _release, _environment);
        if (rebuilt is null) return;
        var processedEnvelope = ProcessEnvelope(rebuilt);
        if (processedEnvelope.Items.Count > 0)
        {
            try
            {
                using var request = CreateRequest(processedEnvelope);
                using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
                await HandleResponseAsync(response, processedEnvelope, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception)
            {
                // Deliberate drop: crash reporting must never crash or block the app.
            }
        }
    }

    public static Envelope? RebuildToGenericEvent(Envelope envelope, string release, string environment)
    {
        if (!envelope.Items.Any(item => item.TryGetType() == "event")) return null;
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
        var header = new Dictionary<string, object?> { ["event_id"] = fresh.EventId.ToString() };
        return new Envelope(header, new[] { EnvelopeItem.FromEvent(fresh) });
    }
}
