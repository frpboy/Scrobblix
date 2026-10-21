using Scrobblix.Application.Abstractions;
using Windows.Security.Credentials;

namespace Scrobblix.Windows;

public sealed class WindowsCredentialStore : ICredentialStore
{
    private const int NotFoundHResult = unchecked((int)0x80070490);
    private readonly PasswordVault _vault = new();

    public async Task StoreAsync(string resource, string userName, string secret, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await DeleteAsync(resource, userName, cancellationToken);
        _vault.Add(new PasswordCredential(resource, userName, secret));
    }

    public Task<string?> GetAsync(string resource, string userName, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var credential = _vault.Retrieve(resource, userName);
            credential.RetrievePassword();
            return Task.FromResult<string?>(credential.Password);
        }
        catch (Exception exception) when (exception.HResult == NotFoundHResult)
        {
            return Task.FromResult<string?>(null);
        }
    }

    public Task DeleteAsync(string resource, string userName, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            _vault.Remove(_vault.Retrieve(resource, userName));
        }
        catch (Exception exception) when (exception.HResult == NotFoundHResult)
        {
        }

        return Task.CompletedTask;
    }
}
