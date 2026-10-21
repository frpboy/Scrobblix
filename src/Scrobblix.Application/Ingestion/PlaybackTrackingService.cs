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

    public async Task<PlaybackTrackingStatus> ObserveAsync(PlayObservation observation, long monotonicMilliseconds, CancellationToken cancellationToken)
    {
        var didStartPlay = false;
        var trackIdentity = GetTrackIdentity(observation);
        if (!string.Equals(trackIdentity, _trackIdentity, StringComparison.Ordinal))
        {
            _tracker = new CanonicalPlayTracker();
            _trackIdentity = trackIdentity;
            _playId = Guid.NewGuid().ToString("N");
            _firstObservedUtc = observation.ObservedAtUtc;
            _persistedEligiblePlay = false;
            didStartPlay = true;
        }

        var decision = _tracker.Observe(observation, monotonicMilliseconds);
        var didPersistEligiblePlay = decision.IsEligible && !_persistedEligiblePlay;
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
            null,
            observation.ArtworkPath);
        await localPlayStore.UpsertAsync(record, cancellationToken);
        if (didPersistEligiblePlay)
        {
            _persistedEligiblePlay = true;
        }

        return new PlaybackTrackingStatus(decision, _tracker.ConfirmedPlayedMilliseconds, didPersistEligiblePlay, didStartPlay);
    }

    private static string GetTrackIdentity(PlayObservation observation) => string.Join(
        "\u001f",
        observation.NormalizedArtist?.ComparisonKey ?? observation.Artist ?? string.Empty,
        observation.NormalizedTitle?.ComparisonKey ?? observation.Title ?? string.Empty,
        observation.DurationMilliseconds?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty);
}
