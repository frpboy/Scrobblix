using Sentry;
using Sentry.Extensibility;
using Sentry.Http;
using Sentry.Protocol.Envelopes;

namespace Scrobblix.Windows;

/// <summary>
/// The final transport gate, independent of every hook: whatever envelope reaches
/// the wire is rebuilt to event items only, under a fresh constant header
/// (event id; sent_at is appended fresh at serialization; no trace context,
/// nothing else). Non-event envelopes (sessions, client reports) are dropped.
/// Mirrors Sentry 7.0.0's internal HttpTransport send flow over the public
/// HttpTransportBase.
/// </summary>
internal sealed class FilteringEnvelopeTransport : HttpTransportBase, ITransport
{
    private readonly HttpClient _httpClient;

    public FilteringEnvelopeTransport(SentryOptions options, HttpClient httpClient) : base(options) => _httpClient = httpClient;

    public async Task SendEnvelopeAsync(Envelope envelope, CancellationToken cancellationToken = default)
    {
        var rebuilt = RebuildToEventsOnly(envelope);
        if (rebuilt is null) return;
        var processedEnvelope = ProcessEnvelope(rebuilt);
        if (processedEnvelope.Items.Count > 0)
        {
            using var request = CreateRequest(processedEnvelope);
            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            await HandleResponseAsync(response, processedEnvelope, cancellationToken).ConfigureAwait(false);
        }
    }

    internal static Envelope? RebuildToEventsOnly(Envelope envelope)
    {
        var eventItems = envelope.Items.Where(item => item.TryGetType() == "event").ToList();
        if (eventItems.Count == 0) return null;
        var header = new Dictionary<string, object?>();
        if (envelope.Header.TryGetValue("event_id", out var eventId) && eventId is not null)
        {
            header["event_id"] = eventId;
        }
        return new Envelope(header, eventItems);
    }
}
