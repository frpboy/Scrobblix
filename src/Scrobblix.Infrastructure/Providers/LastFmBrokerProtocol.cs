namespace Scrobblix.Infrastructure.Providers;

internal static class LastFmBrokerProtocol
{
    internal static void ValidateEndpoint(Uri endpoint)
    {
        if (!endpoint.IsAbsoluteUri || endpoint.Scheme != Uri.UriSchemeHttps ||
            !string.IsNullOrEmpty(endpoint.UserInfo) || !string.IsNullOrEmpty(endpoint.Query) ||
            !string.IsNullOrEmpty(endpoint.Fragment) || endpoint.AbsolutePath != "/v1/lastfm" ||
            endpoint.IsLoopback || !endpoint.IsDefaultPort)
            throw new ArgumentException("A fixed HTTPS broker endpoint is required.", nameof(endpoint));
    }
}
