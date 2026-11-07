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
        // Tolerate JSON-style carriage too: "sessionKey":"value" has a quote between the
        // key and the separator, and the value itself may be quoted. The #75 canary tests
        // pin every one of these forms.
        normalized = Regex.Replace(normalized,
            "(?i)(api[_-]?key|api[_-]?sig|shared[_-]?secret|password|session[_-]?key|token|sk)[\"']?\\s*[=:]\\s*(?:\"(?:\\\\.|[^\"\\\\])*\"|'(?:\\\\.|[^'\\\\])*'|[^&\\s,;\"'}]+)",
            "$1=[REDACTED]");
        normalized = Regex.Replace(normalized,
            @"(?i)https://www\.last\.fm/api/auth/\?[^\s]+",
            "[AUTHORIZATION_URL_REDACTED]");
        // Consume quoted metadata before interpreting semicolons as field boundaries.
        // Unterminated quoted values are redacted to the end rather than leaked.
        normalized = Regex.Replace(normalized,
            "(?i)\\b(artist|title|track|album|album_artist)\\s*[=:]\\s*\"(?:\\\\.|[^\"\\\\])*(?:\"|$)",
            "$1=[REDACTED]");
        normalized = Regex.Replace(normalized,
            @"(?i)\b(artist|title|track|album|album_artist)\s*[=:]\s*.*?(?=;\s*[a-z][a-z0-9_]*\s*[=:]|$)",
            "$1=[REDACTED]");
        return normalized.Length <= maximumLength ? normalized : normalized[..maximumLength] + "…";
    }
}
