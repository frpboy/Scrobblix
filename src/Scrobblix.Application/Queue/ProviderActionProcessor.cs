using Scrobblix.Application.Abstractions;
using Scrobblix.Application.Providers;
using Scrobblix.Domain.Diagnostics;
using Scrobblix.Domain.Queue;

namespace Scrobblix.Application.Queue;

public sealed class ProviderActionProcessor(IProviderActionStore actionStore, IScrobbleProvider provider, Action<string>? log = null)
{
    public async Task<bool> ProcessNextAsync(
        string workerId,
        string sessionKey,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionKey);
        var action = await actionStore.ClaimNextDueAsync(workerId, nowUtc, TimeSpan.FromMinutes(1), cancellationToken);
        if (action is null)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(action.Artist) || string.IsNullOrWhiteSpace(action.Title))
        {
            var metadataReason = string.IsNullOrWhiteSpace(action.Artist) ? ReasonCodes.Metadata.MissingArtist : ReasonCodes.Metadata.MissingTitle;
            await actionStore.CompleteAsync(action.ActionId, ProviderActionState.PermanentFailure, metadataReason, nowUtc, cancellationToken);
            log?.Invoke($"Provider action was not sent because metadata is incomplete; action_id={action.ActionId}; play_id={action.PlayId}; provider={action.Provider}; attempt={action.AttemptCount}; outcome=PermanentFailure; reason={metadataReason}.");
            return true;
        }

        log?.Invoke($"Provider action submission started; action_id={action.ActionId}; play_id={action.PlayId}; provider={action.Provider}; account={action.AccountId}; artist={action.Artist}; title={action.Title}; album={action.Album}; attempt={action.AttemptCount}; started_utc={action.StartedUtc:O}.");

        var submission = new ScrobbleSubmission(
            action.Artist, action.Title, action.StartedUtc, sessionKey, action.Album,
            action.DurationMilliseconds / 1000);
        var result = action.ActionType switch
        {
            ProviderActionType.Scrobble => await provider.SubmitScrobbleAsync(submission, cancellationToken),
            ProviderActionType.NowPlaying => await provider.UpdateNowPlayingAsync(submission, cancellationToken),
            ProviderActionType.Love => await provider.LoveTrackAsync(submission, cancellationToken),
            ProviderActionType.Unlove => await provider.UnloveTrackAsync(submission, cancellationToken),
            _ => new ProviderSubmissionResult(false, ReasonCodes.Provider.ResponseInvalid, false),
        };
        var outcome = result.Accepted
            ? ProviderActionState.Accepted
            : result.ReasonCode == ReasonCodes.Provider.AuthInvalidSession
                ? ProviderActionState.AuthBlocked
                : result.ReasonCode == ReasonCodes.Provider.SubmissionOutcomeUnknown
                    ? ProviderActionState.SubmissionOutcomeUnknown
                    : result.IsTransient
                        ? ProviderActionState.RetryScheduled
                        : ProviderActionState.PermanentFailure;
        await actionStore.CompleteAsync(action.ActionId, outcome, result.ReasonCode, nowUtc, cancellationToken, result.RetryAfter);
        log?.Invoke($"Scrobble delivery finished; action_id={action.ActionId}; play_id={action.PlayId}; provider={action.Provider}; account={action.AccountId}; artist={action.Artist}; title={action.Title}; album={action.Album}; attempt={action.AttemptCount}; outcome={outcome}; accepted={result.Accepted}; transient={result.IsTransient}; reason={result.ReasonCode}; response_utc={nowUtc:O}.");
        return true;
    }
}
