using Scrobblix.Domain.Playback;

namespace Scrobblix.Desktop;

internal sealed record SourceIdentity(string DisplayName, string IconGlyph, string? LogoAssetPath);

/// <summary>
/// Resolves labels for observations already accepted by the desktop pipeline.
/// It does not start adapters, fetch provider logos, or modify tracking and scrobbling.
/// </summary>
internal static class SourceIdentityRegistry
{
    private static readonly SourceIdentity WindowsMedia = new("Windows media controls", "\uE189", null);
    private static readonly SourceIdentity BrowserCompanion = new("Browser Companion", "\uE774", null);
    private static readonly SourceIdentity Unknown = new("Unknown source", "\uE946", null);

    public static SourceIdentity Resolve(PlayObservation observation)
    {
        ArgumentNullException.ThrowIfNull(observation);
        if (string.Equals(observation.SourceName, "gsmtc", StringComparison.OrdinalIgnoreCase))
            return ResolveWindowsApplicationId(observation.ApplicationId);
        if (string.Equals(observation.SourceName, "browser", StringComparison.OrdinalIgnoreCase))
            return ResolveBrowserOrigin(observation.ApplicationId);
        return Unknown;
    }

    public static SourceIdentity ResolveWindowsApplicationId(string? applicationId)
    {
        var id = applicationId?.Trim();
        if (string.Equals(id, "Spotify.exe", StringComparison.OrdinalIgnoreCase)
            || string.Equals(id, "SpotifyAB.SpotifyMusic_zpdnekdrzrea0!Spotify", StringComparison.OrdinalIgnoreCase))
            return WithOptionalLogo("Spotify", "\uE768", "Assets/providers/spotify-full.svg");
        return WindowsMedia;
    }

    public static SourceIdentity ResolveBrowserOrigin(string? origin)
    {
        if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri)
            || !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            || !string.IsNullOrEmpty(uri.UserInfo)
            || uri.Port != 443
            || uri.AbsolutePath != "/"
            || !string.IsNullOrEmpty(uri.Query)
            || !string.IsNullOrEmpty(uri.Fragment))
            return BrowserCompanion;

        return uri.Host.ToLowerInvariant() switch
        {
            "open.spotify.com" => WithOptionalLogo("Spotify", "\uE768", "Assets/providers/spotify-full.svg"),
            "music.youtube.com" => WithOptionalLogo("YouTube Music", "\uE189", "Assets/providers/youtube-music-full.svg"),
            "www.jiosaavn.com" or "jiosaavn.com" => WithOptionalLogo("JioSaavn", "\uE189", "Assets/providers/jiosaavn-full.svg"),
            _ => BrowserCompanion,
        };
    }

    private static SourceIdentity WithOptionalLogo(string name, string glyph, string assetRelativePath)
    {
        var path = Path.Combine(AppContext.BaseDirectory, assetRelativePath.Replace('/', Path.DirectorySeparatorChar));
        return new SourceIdentity(name, glyph, File.Exists(path) ? assetRelativePath : null);
    }
}
