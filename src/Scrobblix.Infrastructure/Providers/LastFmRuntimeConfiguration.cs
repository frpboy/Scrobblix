using System.Diagnostics.CodeAnalysis;

namespace Scrobblix.Infrastructure.Providers;

public sealed record LastFmRuntimeConfiguration(string ApiKey, string ApiSecret)
{
    public static bool TryCreate(string? apiKey, string? apiSecret, [NotNullWhen(true)] out LastFmRuntimeConfiguration? configuration)
    {
        if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(apiSecret))
        {
            configuration = null;
            return false;
        }

        configuration = new LastFmRuntimeConfiguration(apiKey, apiSecret);
        return true;
    }
}
