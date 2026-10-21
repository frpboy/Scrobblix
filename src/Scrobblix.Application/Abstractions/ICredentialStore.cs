namespace Scrobblix.Application.Abstractions;

public interface ICredentialStore
{
    Task StoreAsync(string resource, string userName, string secret, CancellationToken cancellationToken);
    Task<string?> GetAsync(string resource, string userName, CancellationToken cancellationToken);
    Task DeleteAsync(string resource, string userName, CancellationToken cancellationToken);
}
