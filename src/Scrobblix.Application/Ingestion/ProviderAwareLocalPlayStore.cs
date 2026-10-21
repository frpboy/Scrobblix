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
        if (!play.Eligibility.IsEligible)
        {
            await localPlayStore.UpsertAsync(play, cancellationToken);
            return;
        }

        var account = await accountStore.GetConnectedAsync("lastfm", cancellationToken);
        if (account is null)
        {
            await localPlayStore.UpsertAsync(play, cancellationToken);
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

    public Task UpdateMetadataAsync(string playId, Scrobblix.Domain.Metadata.PlayMetadataEnrichment metadata, CancellationToken cancellationToken) =>
        localPlayStore.UpdateMetadataAsync(playId, metadata, cancellationToken);
}
