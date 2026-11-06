using Android.App;
using Android.Content;
using Android.Media;
using Android.Media.Session;
using Scrobblix.Application.Abstractions;
using Scrobblix.Application.Android;
using Scrobblix.Domain.History;

namespace Scrobblix.Android;

public enum MediaSessionAccessStatus { NotChecked, Granted, NotificationAccessRequired }

/// <summary>
/// Reads only structured media-session metadata after the user grants notification-listener access.
/// Does not request/enable access, read notification text, start a service, or control another player.
/// A real app must supply its own declared listener component and surface AccessStatus separately.
/// </summary>
public sealed class AndroidMediaSessionObservationSource(
    Context context, ComponentName listenerComponent, IWallClock clock) : IPlaybackObservationSource
{
    private readonly MediaSessionSnapshotMapper _mapper = new(Guid.NewGuid().ToString("N"));
    public MediaSessionAccessStatus AccessStatus { get; private set; }

    public Task<PlayObservation?> GetCurrentAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var notifications = (NotificationManager?)context.GetSystemService(Context.NotificationService);
        if (OperatingSystem.IsAndroidVersionAtLeast(27)
            && notifications?.IsNotificationListenerAccessGranted(listenerComponent) != true)
        {
            AccessStatus = MediaSessionAccessStatus.NotificationAccessRequired;
            return Task.FromResult<PlayObservation?>(null);
        }

        try
        {
            var sessions = (MediaSessionManager?)context.GetSystemService(Context.MediaSessionService);
            var controllers = sessions?.GetActiveSessions(listenerComponent);
            AccessStatus = MediaSessionAccessStatus.Granted;
            // Platform priority order is preserved among playing sessions. Do not join metadata
            // across apps or interpret notification text when a player exposes no media session.
            var current = controllers?.FirstOrDefault(item => item.PlaybackState?.State == PlaybackStateCode.Playing)
                ?? controllers?.FirstOrDefault();
            if (current is null) return Task.FromResult<PlayObservation?>(null);
            var metadata = current.Metadata;
            var state = current.PlaybackState;
            cancellationToken.ThrowIfCancellationRequested();
            var snapshot = new MediaSnapshot(current.PackageName ?? string.Empty,
                metadata?.GetString(MediaMetadata.MetadataKeyArtist),
                metadata?.GetString(MediaMetadata.MetadataKeyTitle),
                metadata?.GetString(MediaMetadata.MetadataKeyAlbum),
                metadata?.GetLong(MediaMetadata.MetadataKeyDuration), state?.Position,
                state?.State switch
                {
                    PlaybackStateCode.Playing => MediaState.Playing,
                    PlaybackStateCode.Paused => MediaState.Paused,
                    PlaybackStateCode.Stopped => MediaState.Stopped,
                    PlaybackStateCode.Buffering => MediaState.Buffering,
                    _ => MediaState.Unknown,
                });
            return Task.FromResult(_mapper.Map(snapshot, clock.UtcNow));
        }
        catch (Java.Lang.SecurityException)
        {
            // Access may be revoked between the preflight and the binder query.
            AccessStatus = MediaSessionAccessStatus.NotificationAccessRequired;
            return Task.FromResult<PlayObservation?>(null);
        }
    }
}
