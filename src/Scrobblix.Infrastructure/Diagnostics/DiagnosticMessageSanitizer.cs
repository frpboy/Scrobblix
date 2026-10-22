using System.Text.RegularExpressions;

namespace Scrobblix.Infrastructure.Diagnostics;

/// <summary>
/// Redacts credentials and listening metadata from local diagnostic messages.
/// </summary>
public static class DiagnosticMessageSanitizer
{
    public static string Sanitize(string? value, int maximumLength = 6000)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumLength, 1);
        var normalized = (value ?? string.Empty)
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Replace('\t', ' ');
        normalized = Regex.Replace(normalized,
            @"(?i)(api[_-]?key|api[_-]?sig|shared[_-]?secret|password|session[_-]?key|token|sk)\s*[=:]\s*[^&\s,;]+",
            "$1=[REDACTED]");
        normalized = Regex.Replace(normalized,
            @"(?i)https://www\.last\.fm/api/auth/\?[^\s]+",
            "[AUTHORIZATION_URL_REDACTED]");
        normalized = Regex.Replace(normalized,
            @"(?i)\b(artist|title|track|album|album_artist)\s*[=:]\s*[^;,|]+",
            "$1=[REDACTED]");
        return normalized.Length <= maximumLength ? normalized : normalized[..maximumLength] + "…";
    }
}
