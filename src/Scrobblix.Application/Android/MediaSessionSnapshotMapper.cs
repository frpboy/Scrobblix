using Scrobblix.Domain.History;
using Scrobblix.Domain.Metadata;
using Scrobblix.Domain.Playback;

namespace Scrobblix.Application.Android;

public enum MediaState { Unknown, Playing, Paused, Stopped, Buffering }
public sealed record MediaSnapshot(string PackageName, string? Artist, string? Title, string? Album, long? DurationMs, long? PositionMs, MediaState State);
public sealed class MediaSessionSnapshotMapper(string instanceId)
{
    private long sequence;
    // This boundary accepts structured player metadata only, never notification bodies.
    public PlayObservation? Map(MediaSnapshot? value, DateTimeOffset receivedUtc)
    {
        if (value is null || string.IsNullOrWhiteSpace(value.PackageName)) return null;
        if (new[] { value.PackageName, value.Artist, value.Title, value.Album }.Any(
            text => text?.Length > 1024 || text?.Any(char.IsControl) == true)) return null;
        return new PlayObservation(new SourceEventKey(instanceId, value.PackageName, Interlocked.Increment(ref sequence)),
            receivedUtc, receivedUtc, "android-media-session", value.PackageName, value.Artist, value.Title, value.Album,
            value.DurationMs is > 0 and <= int.MaxValue ? (int)value.DurationMs : null,
            value.PositionMs is >= 0 ? value.PositionMs : null,
            value.State switch { MediaState.Playing => PlayState.Playing, MediaState.Paused => PlayState.Paused, MediaState.Stopped => PlayState.Ended, _ => PlayState.Unknown },
            MetadataNormalizer.Normalize(value.Artist), MetadataNormalizer.Normalize(value.Title));
    }
}
