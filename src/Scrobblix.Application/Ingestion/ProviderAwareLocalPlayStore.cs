using Scrobblix.Application.Abstractions;
using Scrobblix.Domain.History;

namespace Scrobblix.Application.Ingestion;

public sealed class ProviderAwareLocalPlayStore(
    ILocalPlayStore localPlayStore,
    IEligiblePlayStore eligiblePlayStore,
    IProviderAccountStore accountStore) : ILocalPlayStore
{
    public async Task UpsertEligibleAsync(LocalPlayRecord play, CancellationToken cancellationToken)
    {
        var account = await accountStore.GetConnectedAsync("lastfm", cancellationToken);
        if (account is null)
        {
            await localPlayStore.UpsertEligibleAsync(play, cancellationToken);
            return;
        }

        await eligiblePlayStore.TryPersistAndQueueAsync(
            play,
            account.Provider,
            account.AccountId,
            play.LastObservedUtc,
            cancellationToken);
    }
}
