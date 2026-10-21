using Scrobblix.Application.Abstractions;
using Scrobblix.Domain.History;

namespace Scrobblix.Application.Ingestion;

public sealed class ProviderAwareLocalPlayStore(
    ILocalPlayStore localPlayStore,
    IEligiblePlayStore eligiblePlayStore,
    IProviderAccountStore accountStore) : ILocalPlayStore
{
    public async Task UpsertAsync(LocalPlayRecord play, CancellationToken cancellationToken)
    {
        await localPlayStore.UpsertAsync(play, cancellationToken);
        if (!play.Eligibility.IsEligible)
        {
            return;
        }

        var account = await accountStore.GetConnectedAsync("lastfm", cancellationToken);
        if (account is null)
        {
            return;
        }

        await eligiblePlayStore.TryPersistAndQueueAsync(
            play,
            account.Provider,
            account.AccountId,
            play.LastObservedUtc,
            cancellationToken);
    }

    public Task UpsertEligibleAsync(LocalPlayRecord play, CancellationToken cancellationToken) =>
        UpsertAsync(play, cancellationToken);
}
