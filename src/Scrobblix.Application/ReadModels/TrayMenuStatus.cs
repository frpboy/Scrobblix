using Scrobblix.Domain.Queue;
using Scrobblix.Domain.Playback;

namespace Scrobblix.Application.ReadModels;

/// <summary>
/// Immutable, local-only snapshot used to render the Windows notification-area menu.
/// Values are copied from existing application read models; constructing this snapshot
/// never queries storage or Last.fm.
/// </summary>
public sealed record TrayMenuStatus(
    bool TrackingEnabled,
    bool LastFmConnected,
    string? LastFmUserName,
    PlayState? PlaybackState,
    string? PlayId,
    string? Artist,
    string? Title,
    ProviderActionType? CurrentLoveIntent,
    bool LoveActionInProgress,
    int? PendingQueueCount,
    int? RetryableFailedScrobbleCount,
    bool QueueOperationInProgress,
    bool IsClosed = false)
{
    public bool HasTrackMetadata =>
        !string.IsNullOrWhiteSpace(Artist) && !string.IsNullOrWhiteSpace(Title);

    public bool HasActiveTrack => PlaybackState is PlayState.Playing or PlayState.Paused;

    public bool HasCurrentTrack => !IsClosed && HasActiveTrack && HasTrackMetadata;

    public bool HasCurrentPlay =>
        HasCurrentTrack && !string.IsNullOrWhiteSpace(PlayId);

    public bool CanLoveTrack =>
        HasCurrentPlay && LastFmConnected && !LoveActionInProgress;

    public bool CanCopyTrackDetails => HasCurrentTrack;

    public bool CanRetryFailedScrobbles =>
        !IsClosed && LastFmConnected && !QueueOperationInProgress && RetryableFailedScrobbleCount is > 0;

    public string TrackingCommandLabel => TrackingEnabled ? "Pause Tracking" : "Resume Tracking";

    public string LastFmLabel => !LastFmConnected
        ? "Last.fm: Not connected"
        : !string.IsNullOrWhiteSpace(LastFmUserName)
            ? $"Last.fm: Connected as {Truncate(LastFmUserName, 42)}"
            : "Last.fm: Connected (account name unavailable)";

    public string PlaybackLabel => PlaybackState switch
    {
        PlayState.Playing => HasTrackMetadata
            ? $"Now Playing: {Truncate(Artist, 34)} — {Truncate(Title, 42)}"
            : "Now Playing: Metadata unavailable",
        PlayState.Paused => HasTrackMetadata
            ? $"Paused: {Truncate(Artist, 34)} — {Truncate(Title, 42)}"
            : "Playback paused",
        _ => "Nothing playing",
    };

    public string QueueLabel => PendingQueueCount is int count
        ? $"Scrobble queue: {Math.Max(0, count):N0} pending"
        : "Scrobble queue: status unavailable";

    public string QueueRetryStatusLabel => RetryableFailedScrobbleCount is int retryable
        ? retryable > 0
            ? $"Retryable failed scrobbles: {retryable:N0}"
            : "No retryable failed scrobbles"
        : "Retryable failures: status unavailable";

    public string CurrentTrackStatusLabel
    {
        get
        {
            if (!HasCurrentTrack) return "Current track: metadata unavailable";
            if (!LastFmConnected) return "Connect Last.fm to manage this track";
            if (LoveActionInProgress) return "Love action in progress";
            return CurrentLoveIntent switch
            {
                ProviderActionType.Love => "Latest intent: Love requested; provider result appears in Loved",
                ProviderActionType.Unlove => "Latest intent: Unlove requested; provider result appears in Loved",
                _ => "Love state has not been confirmed by this session",
            };
        }
    }

    public string LoveCommandLabel => CurrentLoveIntent == ProviderActionType.Love
        ? "Unlove Track"
        : "Love Track";

    public string TrackDetails => HasCurrentTrack
        ? $"{Artist!.Trim()} — {Title!.Trim()}"
        : string.Empty;

    public static string Truncate(string? value, int maximumLength)
    {
        if (maximumLength < 1) return string.Empty;
        var normalized = value?.Trim();
        if (string.IsNullOrEmpty(normalized)) return "Unknown";
        return normalized.Length <= maximumLength
            ? normalized
            : normalized[..Math.Max(0, maximumLength - 1)] + "…";
    }
}
