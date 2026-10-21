using Scrobblix.Application.Abstractions;
using Scrobblix.Domain.History;
using Scrobblix.Domain.Metadata;
using Scrobblix.Domain.Playback;
using Windows.Media.Control;

namespace Scrobblix.Windows;

public sealed class GsmTcPlaybackObservationSource(IWallClock clock) : IPlaybackObservationSource
{
    private long _sequence;

    public async Task<PlayObservation?> GetCurrentAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
        var session = manager.GetCurrentSession();
        if (session is null)
        {
            return null;
        }

        var media = await session.TryGetMediaPropertiesAsync();
        var playback = session.GetPlaybackInfo();
        var timeline = session.GetTimelineProperties();
        var capturedUtc = clock.UtcNow;
        return new PlayObservation(
            new SourceEventKey(session.SourceAppUserModelId, session.SourceAppUserModelId, Interlocked.Increment(ref _sequence)),
            capturedUtc,
            capturedUtc,
            "gsmtc",
            session.SourceAppUserModelId,
            media.Artist,
            media.Title,
            media.AlbumTitle,
            timeline.EndTime > TimeSpan.Zero ? (int?)timeline.EndTime.TotalMilliseconds : null,
            timeline.Position >= TimeSpan.Zero ? (long?)timeline.Position.TotalMilliseconds : null,
            MapState(playback.PlaybackStatus),
            MetadataNormalizer.Normalize(media.Artist),
            MetadataNormalizer.Normalize(media.Title));
    }

    private static PlayState MapState(GlobalSystemMediaTransportControlsSessionPlaybackStatus state) => state switch
    {
        GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing => PlayState.Playing,
        GlobalSystemMediaTransportControlsSessionPlaybackStatus.Paused => PlayState.Paused,
        GlobalSystemMediaTransportControlsSessionPlaybackStatus.Stopped => PlayState.Ended,
        _ => PlayState.Unknown,
    };
}
