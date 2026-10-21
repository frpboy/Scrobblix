using Scrobblix.Domain.Queue;

namespace Scrobblix.Application.Abstractions;

public interface IProviderActionStore
{
    Task<ProviderActionLease?> ClaimNextDueAsync(
        string workerId,
        DateTimeOffset nowUtc,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken);

    Task CompleteAsync(
        string actionId,
        ProviderActionState outcome,
        string reasonCode,
        DateTimeOffset completedUtc,
        CancellationToken cancellationToken);
}
