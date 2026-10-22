using System.Globalization;
using Scrobblix.Application.Ingestion;
using Scrobblix.Domain.Diagnostics;
using Scrobblix.Domain.History;
using Scrobblix.Domain.Playback;
using Scrobblix.Domain.Queue;

namespace Scrobblix.Application.ReadModels;

/// <summary>
/// Produces user-facing tracking/delivery state without implying that eligibility
/// means a provider accepted a scrobble. Kept in the application layer so the
/// status rules can be validated without launching the WinUI desktop app.
/// </summary>
public static class PlaybackStatusPresenter
{
    public static string GetScrobbleStatus(
        PlayObservation? observation,
        PlaybackTrackingStatus? trackingStatus,
        bool trackingEnabled,
        bool lastFmConnected,
        IReadOnlyList<QueueActionSummary> queueActions)
    {
        ArgumentNullException.ThrowIfNull(queueActions);

        if (!trackingEnabled)
        {
            return "Tracking paused";
        }

        if (observation is null || trackingStatus is null)
        {
            return "Waiting for supported playback";
        }

        var playId = trackingStatus.PlayId;
        var action = string.IsNullOrWhiteSpace(playId)
            ? null
            : queueActions
                .Where(item => string.Equals(item.PlayId, playId, StringComparison.Ordinal)
                    && string.Equals(item.Provider, "lastfm", StringComparison.OrdinalIgnoreCase)
                    && item.ActionType == ProviderActionType.Scrobble)
                .OrderByDescending(item => item.AttemptCount)
                .ThenByDescending(item => item.DueUtc)
                .FirstOrDefault();

        if (action is not null)
        {
            return action.State switch
            {
                ProviderActionState.Accepted => "Sent to Last.fm",
                ProviderActionState.Pending or ProviderActionState.InFlight or ProviderActionState.RetryScheduled
                    => "Queued safely — waiting for Last.fm",
                ProviderActionState.AuthBlocked
                    => "Queued safely — reconnect Last.fm to resume delivery",
                ProviderActionState.ProviderIgnored
                    => "Not scrobbled — Last.fm ignored this track",
                ProviderActionState.PermanentFailure or ProviderActionState.RetryExhausted
                    => "Delivery needs attention — open Queue for recovery options",
                ProviderActionState.SubmissionOutcomeUnknown
                    => "Delivery uncertain — check Last.fm and review Queue before retrying",
                ProviderActionState.Cancelled => "Queue action cancelled",
                _ => "Last.fm delivery status is being checked",
            };
        }

        var decision = trackingStatus.Decision;
        if (decision.IsEligible)
        {
            return lastFmConnected
                ? "Eligible — checking Last.fm delivery status"
                : "Eligible — connect Last.fm to submit this play";
        }

        if (decision.ReasonCode == ReasonCodes.Playback.Advertisement)
        {
            return "Not eligible — advertisement playback is ignored";
        }

        if (decision.ReasonCode == ReasonCodes.Playback.TrackTooShort)
        {
            return "Not eligible — this track is too short to scrobble";
        }

        if (decision.ReasonCode == ReasonCodes.Duplicate.ProviderActionExists)
        {
            return "A Last.fm action already exists for this play — check Queue";
        }

        if (decision.ThresholdMilliseconds is int threshold && threshold > 0)
        {
            var remaining = Math.Max(0L, threshold - trackingStatus.ConfirmedPlayedMilliseconds);
            if (observation.PlaybackState == PlayState.Ended)
            {
                return "Not eligible — played too briefly";
            }

            if (observation.PlaybackState == PlayState.Paused)
            {
                return $"Paused — eligible in {FormatDuration(remaining)}";
            }

            if (observation.PlaybackState == PlayState.Playing)
            {
                return $"Listening — eligible in {FormatDuration(remaining)}";
            }

            return $"Eligibility needs {FormatDuration(remaining)} more confirmed listening";
        }

        return decision.ReasonCode switch
        {
            ReasonCodes.Metadata.MissingArtist or ReasonCodes.Metadata.MissingTitle
                => "Waiting for track details — artist and title are required",
            ReasonCodes.Playback.DurationUnknown or ReasonCodes.Metadata.InvalidDuration
                => "Waiting for a valid track duration",
            ReasonCodes.Playback.NonMusic
                => "Not eligible — this playback is not classified as music",
            ReasonCodes.Source.ExcludedByUser
                => "Not eligible — this source is excluded in Settings",
            ReasonCodes.Duplicate.MergedSources
                => "Duplicate source event merged — no new scrobble created",
            ReasonCodes.Playback.SourceLostBeforeEligibility
                => "Playback source was lost before eligibility; waiting for another update",
            _ => "Waiting for enough confirmed listening to determine eligibility",
        };
    }

    private static string FormatDuration(long milliseconds) =>
        TimeSpan.FromMilliseconds(Math.Max(0, milliseconds)).ToString(@"m\:ss", CultureInfo.InvariantCulture);
}
