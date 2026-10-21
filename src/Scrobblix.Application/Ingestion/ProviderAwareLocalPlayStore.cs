using System.Collections.Concurrent;
using Scrobblix.Application.Abstractions;
using Scrobblix.Domain.History;

namespace Scrobblix.Application.Ingestion;

public sealed class ProviderAwareLocalPlayStore(
    ILocalPlayStore localPlayStore,
    IEligiblePlayStore eligiblePlayStore,
    IProviderAccountStore accountStore,
    Action<string>? log = null) : ILocalPlayStore
{
    private readonly ConcurrentDictionary<string, byte> _loggedDuplicateQueueInsertions = new(StringComparer.Ordinal);
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
        if (inserted)
        {
            _loggedDuplicateQueueInsertions.TryRemove(play.PlayId, out _);
            log?.Invoke($"Queued eligible play; play_id={play.PlayId}; provider={account.Provider}; account={account.AccountId}; artist={play.Artist ?? "(missing)"}; title={play.Title ?? "(missing)"}; confirmed_ms={play.ConfirmedPlayedMilliseconds}.");
        }
        else if (_loggedDuplicateQueueInsertions.TryAdd(play.PlayId, 0))
        {
            log?.Invoke($"Duplicate queue insertion was blocked by the database idempotency key; subsequent updates for this play will be coalesced in the log; play_id={play.PlayId}; provider={account.Provider}; account={account.AccountId}; artist={play.Artist ?? "(missing)"}; title={play.Title ?? "(missing)"}.");
        }
    }

    public Task UpsertEligibleAsync(LocalPlayRecord play, CancellationToken cancellationToken) =>
        UpsertAsync(play, cancellationToken);

    public Task UpdateMetadataAsync(string playId, Scrobblix.Domain.Metadata.PlayMetadataEnrichment metadata, CancellationToken cancellationToken) =>
        localPlayStore.UpdateMetadataAsync(playId, metadata, cancellationToken);
}
