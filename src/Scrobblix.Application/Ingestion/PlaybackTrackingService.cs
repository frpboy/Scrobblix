using Scrobblix.Application.Abstractions;
using Scrobblix.Domain.Diagnostics;
using Scrobblix.Domain.Eligibility;
using Scrobblix.Domain.History;
using Scrobblix.Domain.Playback;

namespace Scrobblix.Application.Ingestion;

public sealed class PlaybackTrackingService(ILocalPlayStore localPlayStore) : IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private CanonicalPlayTracker _tracker = new();
    private string? _playId;
    private DateTimeOffset _firstObservedUtc;
    private bool _persistedEligiblePlay;
    private PlayObservation? _previousObservation;
    private long _lastObservationMonotonicMilliseconds;

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

    public async Task<(PlayObservation Observation, PlaybackTrackingStatus Status)?> ObserveSourceUnavailableAsync(
        string sourceName,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceName);
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_previousObservation is not { PlaybackState: PlayState.Playing } previous
                || !string.Equals(previous.SourceName, sourceName, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            // Stop at the last positive playback sample, not at the later poll that
            // discovered the source disappeared. The gap itself is not confirmed listening.
            var unavailableObservation = previous with { PlaybackState = PlayState.Unknown };
            var status = await ObserveCoreAsync(
                unavailableObservation,
                _lastObservationMonotonicMilliseconds,
                cancellationToken);
            return (unavailableObservation, status);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<PlaybackTrackingStatus> ObserveCoreAsync(PlayObservation observation, long monotonicMilliseconds, CancellationToken cancellationToken)
    {
        var didStartPlay = false;
        var sameTrack = IsSameTrack(_previousObservation, observation);
        var replayStarted = sameTrack && IsReplay(_previousObservation, observation);
        if (!sameTrack || replayStarted)
        {
            _tracker = new CanonicalPlayTracker();
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
        _lastObservationMonotonicMilliseconds = monotonicMilliseconds;
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

    private static bool IsSameTrack(PlayObservation? previous, PlayObservation current)
    {
        if (previous is null)
        {
            return false;
        }

        var previousTitle = NormalizeForIdentity(previous.NormalizedTitle?.ComparisonKey ?? previous.Title);
        var currentTitle = NormalizeForIdentity(current.NormalizedTitle?.ComparisonKey ?? current.Title);
        if (previousTitle.Length == 0 || !string.Equals(previousTitle, currentTitle, StringComparison.Ordinal))
        {
            return false;
        }

        var previousArtist = NormalizeForIdentity(previous.NormalizedArtist?.ComparisonKey ?? previous.Artist);
        var currentArtist = NormalizeForIdentity(current.NormalizedArtist?.ComparisonKey ?? current.Artist);
        return previousArtist.Length == 0
            || currentArtist.Length == 0
            || string.Equals(previousArtist, currentArtist, StringComparison.Ordinal);
    }

    private static string NormalizeForIdentity(string? value) =>
        string.Concat((value ?? string.Empty).Where(char.IsLetterOrDigit)).ToUpperInvariant();

    public void Dispose() => _gate.Dispose();
}
