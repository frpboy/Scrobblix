using Scrobblix.Domain.History;

namespace Scrobblix.Application.Abstractions;

public interface IEligiblePlayStore
{
    Task<bool> TryPersistAndQueueAsync(
        LocalPlayRecord play,
        string provider,
        string accountId,
        DateTimeOffset queuedUtc,
        CancellationToken cancellationToken);
}
