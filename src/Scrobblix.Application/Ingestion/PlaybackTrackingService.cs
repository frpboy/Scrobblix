using Scrobblix.Application.Abstractions;
using Scrobblix.Domain.Diagnostics;
using Scrobblix.Domain.Eligibility;
using Scrobblix.Domain.History;
using Scrobblix.Domain.Playback;

namespace Scrobblix.Application.Ingestion;

public sealed class PlaybackTrackingService(ILocalPlayStore localPlayStore)
{
    private CanonicalPlayTracker _tracker = new();
    private string? _trackIdentity;
    private string? _playId;
    private DateTimeOffset _firstObservedUtc;
    private bool _persistedEligiblePlay;

    public async Task<EligibilityDecision> ObserveAsync(PlayObservation observation, long monotonicMilliseconds, CancellationToken cancellationToken)
    {
        var trackIdentity = GetTrackIdentity(observation);
        if (!string.Equals(trackIdentity, _trackIdentity, StringComparison.Ordinal))
        {
            _tracker = new CanonicalPlayTracker();
            _trackIdentity = trackIdentity;
            _playId = Guid.NewGuid().ToString("N");
            _firstObservedUtc = observation.ObservedAtUtc;
            _persistedEligiblePlay = false;
        }

        var decision = _tracker.Observe(observation, monotonicMilliseconds);
        if (decision.IsEligible && !_persistedEligiblePlay)
        {
            var record = new LocalPlayRecord(
                _playId!,
                _firstObservedUtc,
                observation.ObservedAtUtc,
                observation.Artist,
                observation.Title,
                observation.Album,
                observation.DurationMilliseconds,
                _tracker.ConfirmedPlayedMilliseconds,
                decision,
                null,
                null,
                null);
            await localPlayStore.UpsertEligibleAsync(record, cancellationToken);
            _persistedEligiblePlay = true;
        }

        return decision;
    }

    private static string GetTrackIdentity(PlayObservation observation) => string.Join(
        "\u001f",
        observation.NormalizedArtist?.ComparisonKey ?? observation.Artist ?? string.Empty,
        observation.NormalizedTitle?.ComparisonKey ?? observation.Title ?? string.Empty,
        observation.DurationMilliseconds?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty);
}
