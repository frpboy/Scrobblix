using Scrobblix.Domain.Queue;

namespace Scrobblix.Application.Abstractions;

public interface IProviderActionStore
{
    Task<int> RecoverExpiredLeasesAsync(DateTimeOffset nowUtc, CancellationToken cancellationToken);

    Task<ProviderActionLease?> ClaimNextDueAsync(
        string workerId,
        DateTimeOffset nowUtc,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken);

    Task<bool> RetryNowAsync(string actionId, DateTimeOffset nowUtc, CancellationToken cancellationToken);

    Task<bool> CancelAsync(string actionId, DateTimeOffset nowUtc, CancellationToken cancellationToken);

    Task<bool> ConfirmOutcomeAcceptedAsync(string actionId, DateTimeOffset nowUtc, CancellationToken cancellationToken);

    Task CompleteAsync(
        string actionId,
        ProviderActionState outcome,
        string reasonCode,
        DateTimeOffset completedUtc,
        CancellationToken cancellationToken,
        TimeSpan? retryAfter = null);
}
