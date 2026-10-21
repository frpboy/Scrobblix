using System.Text.Json;

namespace Scrobblix.NativeHost;

public static class NativeHostCallerValidator
{
    public static bool MatchesExtensionOrigin(string callerOrigin, JsonElement message)
    {
        if (!message.TryGetProperty("browserInstanceId", out var browserInstanceId)
            || browserInstanceId.ValueKind != JsonValueKind.String
            || browserInstanceId.GetString() is not { Length: > 0 and <= 128 } extensionId)
        {
            return false;
        }

        return string.Equals(
            callerOrigin.TrimEnd('/'),
            $"chrome-extension://{extensionId}",
            StringComparison.Ordinal);
    }
}
