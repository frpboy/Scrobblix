using Scrobblix.Application.Providers;

namespace Scrobblix.Application.Abstractions;

public interface IProviderAccountStore
{
    Task UpsertAsync(ProviderAccount account, CancellationToken cancellationToken);

    Task<ProviderAccount?> GetConnectedAsync(string provider, CancellationToken cancellationToken);

    Task DisconnectAsync(string provider, CancellationToken cancellationToken);
}
