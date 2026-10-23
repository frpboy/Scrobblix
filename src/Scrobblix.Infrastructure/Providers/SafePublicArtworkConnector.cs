using System.Net;
using System.Net.Sockets;

namespace Scrobblix.Infrastructure.Providers;

/// <summary>
/// Connects remote artwork HTTP requests only to resolved public unicast addresses.
/// The socket is opened to the exact address that was checked, avoiding a separate
/// validation lookup followed by a second, potentially rebound DNS resolution.
/// </summary>
public static class SafePublicArtworkConnector
{
    public static HttpClient CreateHttpClient(TimeSpan? timeout = null) =>
        new(new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            UseProxy = false,
            ConnectCallback = ConnectAsync,
        })
        {
            Timeout = timeout ?? TimeSpan.FromSeconds(12),
        };

    public static ValueTask<Stream> ConnectAsync(
        SocketsHttpConnectionContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        return ConnectAsync(context.DnsEndPoint, ResolveHostAsync, cancellationToken);
    }

    /// <summary>
    /// Exposed for deterministic regression testing with a controlled resolver.
    /// </summary>
    public static async ValueTask<Stream> ConnectAsync(
        DnsEndPoint endpoint,
        Func<string, CancellationToken, Task<IPAddress[]>> resolveHostAsync,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        ArgumentNullException.ThrowIfNull(resolveHostAsync);
        ArgumentException.ThrowIfNullOrWhiteSpace(endpoint.Host);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(endpoint.Port, 0);

        var addresses = await resolveHostAsync(endpoint.Host, cancellationToken);
        if (addresses.Length == 0 || addresses.Any(address => !IsPublicUnicastAddress(address)))
        {
            throw new HttpRequestException("Artwork host resolved to a non-public or unsupported IP address.");
        }

        SocketException? lastSocketException = null;
        foreach (var address in addresses)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            try
            {
                await socket.ConnectAsync(new IPEndPoint(address, endpoint.Port), cancellationToken);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch (SocketException exception)
            {
                socket.Dispose();
                lastSocketException = exception;
            }
            catch
            {
                socket.Dispose();
                throw;
            }
        }

        throw new HttpRequestException(
            "Artwork host could not be reached at any validated public address.",
            lastSocketException);
    }

    public static bool IsPublicUnicastAddress(IPAddress address)
    {
        ArgumentNullException.ThrowIfNull(address);

        if (address.IsIPv4MappedToIPv6)
        {
            return IsPublicUnicastAddress(address.MapToIPv4());
        }

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var bytes = address.GetAddressBytes();
            var first = bytes[0];
            var second = bytes[1];
            var third = bytes[2];

            if (IPAddress.IsLoopback(address)
                || first == 0
                || first == 10
                || first == 127
                || (first == 100 && second is >= 64 and <= 127)
                || (first == 169 && second == 254)
                || (first == 172 && second is >= 16 and <= 31)
                || (first == 192 && second == 0 && third == 0)
                || (first == 192 && second == 0 && third == 2)
                || (first == 192 && second == 88 && third == 99)
                || (first == 192 && second == 168)
                || (first == 198 && second is 18 or 19)
                || (first == 198 && second == 51 && third == 100)
                || (first == 203 && second == 0 && third == 113)
                || first >= 224)
            {
                return false;
            }

            return true;
        }

        if (address.AddressFamily != AddressFamily.InterNetworkV6)
        {
            return false;
        }

        var ipv6 = address.GetAddressBytes();
        // Only accept globally routable IPv6 unicast space (2000::/3), not
        // unspecified/loopback/ULA/link-local/multicast or transition/documentation ranges.
        if ((ipv6[0] & 0xE0) != 0x20
            || address.IsIPv6Loopback
            || address.IsIPv6LinkLocal
            || address.IsIPv6Multicast
            || address.IsIPv6Teredo
            || (ipv6[0] == 0x20 && ipv6[1] == 0x01 && ipv6[2] == 0x0D && ipv6[3] == 0xB8)
            || (ipv6[0] == 0x20 && ipv6[1] == 0x02)
            || (ipv6[0] == 0x20 && ipv6[1] == 0x01 && ipv6[2] == 0 && ipv6[3] == 0)
            || (ipv6[0] == 0x20 && ipv6[1] == 0x01 && ipv6[2] <= 0x01))
        {
            return false;
        }

        return true;
    }

    private static Task<IPAddress[]> ResolveHostAsync(string host, CancellationToken cancellationToken) =>
        Dns.GetHostAddressesAsync(host, cancellationToken);
}
