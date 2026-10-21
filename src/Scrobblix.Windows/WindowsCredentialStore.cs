using Scrobblix.Application.Abstractions;
using Windows.Security.Credentials;

namespace Scrobblix.Windows;

public sealed class WindowsCredentialStore : ICredentialStore
{
    private readonly PasswordVault _vault = new();

    public Task StoreAsync(string resource, string userName, string secret, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(resource);
        ArgumentException.ThrowIfNullOrWhiteSpace(userName);
        ArgumentException.ThrowIfNullOrWhiteSpace(secret);
        Delete(resource, userName);
        _vault.Add(new PasswordCredential(resource, userName, secret));
        return Task.CompletedTask;
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
        catch (Exception exception) when (exception.HResult == unchecked((int)0x80070490))
        {
            return Task.FromResult<string?>(null);
        }
    }

    public Task DeleteAsync(string resource, string userName, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Delete(resource, userName);
        return Task.CompletedTask;
    }

    private void Delete(string resource, string userName)
    {
        try { _vault.Remove(_vault.Retrieve(resource, userName)); }
        catch (Exception exception) when (exception.HResult == unchecked((int)0x80070490)) { }
    }
}
