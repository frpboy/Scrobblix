using Scrobblix.Application.Abstractions;
using Scrobblix.Domain.History;

namespace Scrobblix.Application.Ingestion;

public sealed class ProviderAwareLocalPlayStore(
    ILocalPlayStore localPlayStore,
    IEligiblePlayStore eligiblePlayStore,
    IProviderAccountStore accountStore,
    Action<string>? log = null) : ILocalPlayStore
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

        var inserted = await eligiblePlayStore.TryPersistAndQueueAsync(
            play,
            account.Provider,
            account.AccountId,
            play.LastObservedUtc,
            cancellationToken);
        log?.Invoke(inserted
            ? $"Queued eligible play; play_id={play.PlayId}; provider={account.Provider}; account={account.AccountId}; artist={play.Artist ?? "(missing)"}; title={play.Title ?? "(missing)"}; confirmed_ms={play.ConfirmedPlayedMilliseconds}."
            : $"Suppressed duplicate queue insertion by idempotency key; play_id={play.PlayId}; provider={account.Provider}; account={account.AccountId}; artist={play.Artist ?? "(missing)"}; title={play.Title ?? "(missing)"}.");
    }

    public Task UpsertEligibleAsync(LocalPlayRecord play, CancellationToken cancellationToken) =>
        UpsertAsync(play, cancellationToken);

    public Task UpdateMetadataAsync(string playId, Scrobblix.Domain.Metadata.PlayMetadataEnrichment metadata, CancellationToken cancellationToken) =>
        localPlayStore.UpdateMetadataAsync(playId, metadata, cancellationToken);
}
