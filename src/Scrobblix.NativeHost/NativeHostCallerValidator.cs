using Microsoft.Win32;
using System.Text.Json;

namespace Scrobblix.NativeHost;

public static class NativeHostCallerValidator
{
    private const string TrustRegistryPath = @"HKEY_CURRENT_USER\Software\Scrobblix\NativeMessaging";

    /// <summary>
    /// Reads the per-user extension IDs explicitly registered by the Scrobblix installer.
    /// An empty allow-list is fail-closed: the native host must not accept playback data
    /// until the user has installed/registered the companion.
    /// </summary>
    public static IReadOnlySet<string> ReadAllowedChromiumExtensionIds()
    {
        if (!OperatingSystem.IsWindows())
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        try
        {
            var configured = Registry.GetValue(TrustRegistryPath, "AllowedChromeExtensionIds", null) as string;
            return (configured ?? string.Empty)
                .Split(new[] { ',', ';', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(IsValidExtensionId)
                .ToHashSet(StringComparer.Ordinal);
        }
        catch (UnauthorizedAccessException)
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }
        catch (System.Security.SecurityException)
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }
    }

    public static bool MatchesExtensionOrigin(string callerOrigin, JsonElement message) =>
        MatchesExtensionOrigin(callerOrigin, message, ReadAllowedChromiumExtensionIds());

    public static bool MatchesExtensionOrigin(
        string callerOrigin,
        JsonElement message,
        IReadOnlySet<string> allowedExtensionIds)
    {
        ArgumentNullException.ThrowIfNull(allowedExtensionIds);
        if (!message.TryGetProperty("browserInstanceId", out var browserInstanceId)
            || browserInstanceId.ValueKind != JsonValueKind.String
            || browserInstanceId.GetString() is not { Length: 32 } extensionId
            || !IsValidExtensionId(extensionId)
            || !allowedExtensionIds.Contains(extensionId))
        {
            return false;
        }

        return string.Equals(
            callerOrigin.TrimEnd('/'),
            $"chrome-extension://{extensionId}",
            StringComparison.Ordinal);
    }

    private static bool IsValidExtensionId(string value) =>
        value.Length == 32 && value.All(character => character is >= 'a' and <= 'p');
}
