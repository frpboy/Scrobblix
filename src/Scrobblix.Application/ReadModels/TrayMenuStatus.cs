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
    ProviderActionState? CurrentLoveActionState,
    bool LoveActionInProgress,
    int? PendingQueueCount,
    int? RetryableFailedScrobbleCount,
    bool QueueOperationInProgress,
    bool IsClosed = false,
    QueueSummary? QueueSnapshot = null)
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

    public IReadOnlyList<string> QueueDetailLabels => QueueSnapshot is { } queue
        ? new[]
        {
            $"In flight: {queue.InFlightCount:N0}",
            $"Retry scheduled: {queue.RetryScheduledCount:N0}",
            $"Accepted: {queue.AcceptedCount:N0}",
            $"Authentication blocked: {queue.AuthBlockedCount:N0}",
            $"Permanent failures: {queue.PermanentFailureCount:N0}",
            $"Submission outcome unknown: {queue.SubmissionOutcomeUnknownCount:N0}",
        }
        : new[] { "Queue details: status unavailable" };

    public string CurrentTrackStatusLabel
    {
        get
        {
            if (!HasCurrentTrack) return "Current track: metadata unavailable";
            if (!LastFmConnected) return "Connect Last.fm to manage this track";
            if (LoveActionInProgress) return "Love action in progress";

            var action = CurrentLoveIntent == ProviderActionType.Unlove ? "Unlove" : "Love";
            return CurrentLoveActionState switch
            {
                ProviderActionState.Accepted when CurrentLoveIntent == ProviderActionType.Love => "Loved on Last.fm",
                ProviderActionState.Accepted when CurrentLoveIntent == ProviderActionType.Unlove => "Unloved on Last.fm",
                ProviderActionState.Pending => $"{action} request queued; waiting for Last.fm",
                ProviderActionState.InFlight => $"{action} request is being sent to Last.fm",
                ProviderActionState.RetryScheduled => $"{action} request scheduled to retry",
                ProviderActionState.RetryExhausted => $"{action} request needs manual retry",
                ProviderActionState.AuthBlocked => $"{action} request blocked by Last.fm authentication",
                ProviderActionState.PermanentFailure => $"Last.fm permanently failed the {action} request",
                ProviderActionState.ProviderIgnored => $"Last.fm ignored the {action} request",
                ProviderActionState.Cancelled => $"{action} request cancelled or superseded",
                ProviderActionState.SubmissionOutcomeUnknown => $"{action} result is unknown; review Loved and Queue",
                _ when CurrentLoveIntent is not null => $"{action} intent saved locally; provider result is not yet available",
                _ => "Love state has not been confirmed for this track",
            };
        }
    }

    public string LoveCommandLabel => !HasCurrentTrack
        ? "Love Track"
        : CurrentLoveIntent == ProviderActionType.Love
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
