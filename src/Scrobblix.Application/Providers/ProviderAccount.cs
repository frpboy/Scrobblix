namespace Scrobblix.Application.Providers;

public sealed record ProviderAccount(
    string AccountId,
    string Provider,
    string UserName,
    string CredentialResource,
    bool IsConnected,
    DateTimeOffset ConnectedUtc);
