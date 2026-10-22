using System.Security.Cryptography;
using System.Text;

namespace Scrobblix.Domain.Metadata;

/// <summary>
/// Stable, privacy-light identity for a track independent of album version or play session.
/// The original artist/title stay in the history table; only this hash is used as the state key.
/// </summary>
public static class TrackKey
{
    public static string Create(string artist, string title)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(artist);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        var normalizedArtist = MetadataNormalizer.Normalize(artist)?.ComparisonKey ?? artist.Trim().ToUpperInvariant();
        var normalizedTitle = MetadataNormalizer.Normalize(title)?.ComparisonKey ?? title.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(normalizedArtist) || string.IsNullOrWhiteSpace(normalizedTitle))
        {
            throw new ArgumentException("A track key requires non-empty normalized artist and title.");
        }

        var payload = string.Concat(normalizedArtist, "\u001f", normalizedTitle);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
    }
}
