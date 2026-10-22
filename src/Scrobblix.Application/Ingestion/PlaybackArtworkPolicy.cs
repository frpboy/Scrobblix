namespace Scrobblix.Application.Ingestion;

/// <summary>
/// Avoid showing browser application icons as if they were album artwork. Browser media sessions
/// often expose the application logo through GSMTC even when the web player provides no cover art.
/// </summary>
public static class PlaybackArtworkPolicy
{
    private static readonly string[] BrowserApplicationHints =
    [
        "chrome",
        "chromium",
        "msedge",
        "microsoftedge",
        "firefox",
        "brave",
        "vivaldi",
        "opera",
    ];

    public static bool IsLikelyBrowserApplication(string? applicationId)
    {
        if (string.IsNullOrWhiteSpace(applicationId))
        {
            return false;
        }

        return BrowserApplicationHints.Any(hint =>
            applicationId.Contains(hint, StringComparison.OrdinalIgnoreCase));
    }
}
