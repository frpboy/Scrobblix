using System.Text.Json;
using Scrobblix.Application.Abstractions;

namespace Scrobblix.Infrastructure.Providers;

public sealed record LastFmAuthorization(string Token, Uri AuthorizationUri);

public sealed record LastFmSession(string UserName);

public sealed class LastFmDesktopAuthenticator(
    HttpClient client,
    string apiKey,
    string apiSecret,
    ICredentialStore credentialStore)
{
    private const string Endpoint = "https://ws.audioscrobbler.com/2.0/";
    private const string CredentialResource = "Scrobblix.LastFm";

    public async Task<LastFmAuthorization> BeginAsync(CancellationToken cancellationToken)
    {
        var token = await PostForPropertyAsync("auth.getToken", null, "token", cancellationToken);
        var authorizationUri = new Uri($"https://www.last.fm/api/auth/?api_key={Uri.EscapeDataString(apiKey)}&token={Uri.EscapeDataString(token)}");
        return new LastFmAuthorization(token, authorizationUri);
    }

    public async Task<LastFmSession> CompleteAsync(string token, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        var parameters = CreateParameters("auth.getSession", token);
        using var response = await client.PostAsync(Endpoint, new FormUrlEncodedContent(parameters), cancellationToken);
        var document = await ReadDocumentAsync(response, cancellationToken);
        var session = document.RootElement.GetProperty("session");
        var userName = session.GetProperty("name").GetString();
        var sessionKey = session.GetProperty("key").GetString();
        if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(sessionKey))
        {
            throw new InvalidOperationException("Last.fm returned an incomplete session.");
        }

        await credentialStore.StoreAsync(CredentialResource, userName, sessionKey, cancellationToken);
        return new LastFmSession(userName);
    }

    private async Task<string> PostForPropertyAsync(string method, string? token, string propertyName, CancellationToken cancellationToken)
    {
        var parameters = CreateParameters(method, token);

        using var response = await client.PostAsync(Endpoint, new FormUrlEncodedContent(parameters), cancellationToken);
        var document = await ReadDocumentAsync(response, cancellationToken);
        var value = document.RootElement.GetProperty(propertyName).GetString();
        return !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new InvalidOperationException($"Last.fm returned no {propertyName}.");
    }

    private Dictionary<string, string> CreateParameters(string method, string? token = null)
    {
        var parameters = new Dictionary<string, string>
        {
            ["method"] = method,
            ["api_key"] = apiKey,
            ["format"] = "json",
        };
        if (token is not null)
        {
            parameters["token"] = token;
        }
        parameters["api_sig"] = LastFmRequestSigner.Sign(parameters, apiSecret);
        return parameters;
    }

    private static async Task<JsonDocument> ReadDocumentAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        response.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
    }
}
