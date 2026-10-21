using System.Security.Cryptography;
using System.Text;

namespace Scrobblix.Infrastructure.Providers;

public static class LastFmRequestSigner
{
    public static string Sign(IReadOnlyDictionary<string, string> parameters, string secret)
    {
        var source = string.Concat(parameters
            .Where(pair => pair.Key != "format" && pair.Key != "api_sig")
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => pair.Key + pair.Value)) + secret;
#pragma warning disable CA5351 // Last.fm's documented API signature protocol requires MD5.
        return Convert.ToHexStringLower(MD5.HashData(Encoding.UTF8.GetBytes(source)));
#pragma warning restore CA5351
    }
}
