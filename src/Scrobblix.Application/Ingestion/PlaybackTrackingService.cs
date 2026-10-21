using Scrobblix.Application.Abstractions;
using Scrobblix.Domain.Diagnostics;
using Scrobblix.Domain.Eligibility;
using Scrobblix.Domain.History;
using Scrobblix.Domain.Playback;

namespace Scrobblix.Application.Ingestion;

public sealed class PlaybackTrackingService(ILocalPlayStore localPlayStore)
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private CanonicalPlayTracker _tracker = new();
    private string? _trackIdentity;
    private string? _playId;
    private DateTimeOffset _firstObservedUtc;
    private bool _persistedEligiblePlay;
    private PlayObservation? _previousObservation;

    public async Task<PlaybackTrackingStatus> ObserveAsync(PlayObservation observation, long monotonicMilliseconds, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            return await ObserveCoreAsync(observation, monotonicMilliseconds, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<PlaybackTrackingStatus> ObserveCoreAsync(PlayObservation observation, long monotonicMilliseconds, CancellationToken cancellationToken)
    {
        var didStartPlay = false;
        var trackIdentity = GetTrackIdentity(observation);
        var replayStarted = string.Equals(trackIdentity, _trackIdentity, StringComparison.Ordinal)
            && IsReplay(_previousObservation, observation);
        if (!string.Equals(trackIdentity, _trackIdentity, StringComparison.Ordinal) || replayStarted)
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
            observation.ArtworkPath,
            observation.AlbumArtist,
            observation.TrackNumber,
            observation.ReleaseYear,
            observation.RecordingMusicBrainzId,
            observation.ReleaseMusicBrainzId,
            observation.ArtworkSource);
        await localPlayStore.UpsertAsync(record, cancellationToken);
        if (didPersistEligiblePlay)
        {
            _persistedEligiblePlay = true;
        }

        _previousObservation = observation;
        return new PlaybackTrackingStatus(decision, _tracker.ConfirmedPlayedMilliseconds, didPersistEligiblePlay, didStartPlay, _playId);
    }

    private static bool IsReplay(PlayObservation? previous, PlayObservation current)
    {
        if (previous is null
            || !string.Equals(previous.SourceName, current.SourceName, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(previous.ApplicationId, current.ApplicationId, StringComparison.Ordinal)
            || current.PlaybackState != PlayState.Playing
            || current.PositionMilliseconds is null or > 5000)
        {
            return false;
        }

        if (previous.PlaybackState == PlayState.Ended)
        {
            return true;
        }

        return previous.PositionMilliseconds is > 60_000
            && previous.PositionMilliseconds.Value - current.PositionMilliseconds.Value >= 60_000;
    }

    private static string GetTrackIdentity(PlayObservation observation) => string.Join(
        "\u001f",
        observation.NormalizedArtist?.ComparisonKey ?? observation.Artist ?? string.Empty,
        observation.NormalizedTitle?.ComparisonKey ?? observation.Title ?? string.Empty);
}
