using Scrobblix.Domain.Queue;

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
    bool IsPlaying,
    string? PlayId,
    string? Artist,
    string? Title,
    ProviderActionType? CurrentLoveIntent,
    bool LoveActionInProgress,
    int? PendingQueueCount,
    int RetryableFailedScrobbleCount,
    bool QueueOperationInProgress,
    bool IsClosed = false)
{
    public bool HasTrackMetadata =>
        !string.IsNullOrWhiteSpace(Artist) && !string.IsNullOrWhiteSpace(Title);

    public bool HasCurrentPlay =>
        !IsClosed && !string.IsNullOrWhiteSpace(PlayId) && HasTrackMetadata;

    public bool CanLoveTrack =>
        HasCurrentPlay && LastFmConnected && !LoveActionInProgress;

    public bool CanCopyTrackDetails => HasTrackMetadata && !IsClosed;

    public bool CanRetryFailedScrobbles =>
        !IsClosed && LastFmConnected && !QueueOperationInProgress && RetryableFailedScrobbleCount > 0;

    public string TrackingCommandLabel => TrackingEnabled ? "Pause Tracking" : "Resume Tracking";

    public string LastFmLabel => LastFmConnected
        ? $"Last.fm: Connected as {Truncate(LastFmUserName, 42)}"
        : "Last.fm: Not connected";

    public string PlaybackLabel => IsPlaying
        ? HasTrackMetadata
            ? $"Now Playing: {Truncate(Artist, 34)} — {Truncate(Title, 42)}"
            : "Now Playing: Metadata unavailable"
        : "Nothing playing";

    public string QueueLabel => PendingQueueCount is int count
        ? $"Scrobble queue: {Math.Max(0, count):N0} pending"
        : "Scrobble queue: status unavailable";

    public string CurrentTrackStatusLabel
    {
        get
        {
            if (!HasTrackMetadata) return "Current track: metadata unavailable";
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

    public string TrackDetails => HasTrackMetadata
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
