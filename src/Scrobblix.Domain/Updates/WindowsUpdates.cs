using System.Text.RegularExpressions;

namespace Scrobblix.Domain.Updates;

/// <summary>Semantic version of a windows-v* release tag. Channel ordering: alpha &lt; beta &lt; rc &lt; stable.
/// Parsing never throws: untrusted tag text that is malformed or overflows returns null.</summary>
public readonly record struct WindowsVersion(int Major, int Minor, int Patch, string? Channel, int? ChannelNumber)
    : IComparable<WindowsVersion>
{
    private static readonly Regex Shape = new(@"^(\d{1,9})\.(\d{1,9})\.(\d{1,9})(?:-(alpha|beta|rc)\.(\d{1,9}))?$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static WindowsVersion? Parse(string text)
    {
        var match = Shape.Match(text.Trim());
        if (!match.Success) return null;
        if (!int.TryParse(match.Groups[1].Value, out var major)) return null;
        if (!int.TryParse(match.Groups[2].Value, out var minor)) return null;
        if (!int.TryParse(match.Groups[3].Value, out var patch)) return null;
        var channel = match.Groups[4].Success ? match.Groups[4].Value : null;
        int? channelNumber = null;
        if (channel is not null)
        {
            if (!int.TryParse(match.Groups[5].Value, out var number)) return null;
            channelNumber = number;
        }
        return new WindowsVersion(major, minor, patch, channel, channelNumber);
    }

    private static int ChannelRank(string? channel) => channel switch
    {
        "alpha" => 0,
        "beta" => 1,
        "rc" => 2,
        null => 3,
        _ => -1,
    };

    public int CompareTo(WindowsVersion other)
    {
        var core = Major.CompareTo(other.Major);
        if (core != 0) return core;
        core = Minor.CompareTo(other.Minor);
        if (core != 0) return core;
        core = Patch.CompareTo(other.Patch);
        if (core != 0) return core;
        core = ChannelRank(Channel).CompareTo(ChannelRank(other.Channel));
        if (core != 0) return core;
        return (ChannelNumber ?? 0).CompareTo(other.ChannelNumber ?? 0);
    }

    public static bool operator >(WindowsVersion left, WindowsVersion right) => left.CompareTo(right) > 0;
    public static bool operator <(WindowsVersion left, WindowsVersion right) => left.CompareTo(right) < 0;
    public static bool operator >=(WindowsVersion left, WindowsVersion right) => left.CompareTo(right) >= 0;
    public static bool operator <=(WindowsVersion left, WindowsVersion right) => left.CompareTo(right) <= 0;

    public override string ToString() =>
        Channel is null ? $"{Major}.{Minor}.{Patch}" : $"{Major}.{Minor}.{Patch}-{Channel}.{ChannelNumber}";
}

public sealed record WindowsReleaseAsset(string Name, string DownloadUrl, long SizeBytes);

public sealed record WindowsRelease(
    string Tag,
    string Title,
    bool Prerelease,
    bool Draft,
    string Notes,
    IReadOnlyList<WindowsReleaseAsset> Assets);

/// <summary>Pure selection over the release list: newest windows-v* tag with an installer asset.
/// No network, no ambient state; channel builds (alpha/beta/rc) are eligible because every
/// published Windows build is a development preview.</summary>
public static class WindowsUpdateCatalog
{
    public const string TagPrefix = "windows-v";
    public const string ChecksumsName = "SHA256SUMS.txt";

    public static WindowsVersion? ParseTag(string tag) =>
        tag.StartsWith(TagPrefix, StringComparison.Ordinal) ? WindowsVersion.Parse(tag[TagPrefix.Length..]) : null;

    public static WindowsRelease? SelectLatest(IEnumerable<WindowsRelease> releases)
    {
        WindowsRelease? best = null;
        WindowsVersion? bestVersion = null;
        foreach (var release in releases)
        {
            if (release.Draft) continue;
            var version = ParseTag(release.Tag);
            if (version is null) continue;
            if (!release.Assets.Any(asset => asset.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))) continue;
            if (best is null || version.Value > bestVersion!.Value)
            {
                best = release;
                bestVersion = version;
            }
        }
        return best;
    }

    /// <summary>Exact "hash  filename" lines only; anything else is not integrity evidence.</summary>
    public static string? ChecksumFor(string content, string assetName)
    {
        foreach (var raw in content.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0) continue;
            var parts = line.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length != 2) continue;
            var name = parts[1].TrimStart('*').Trim();
            if (name == assetName && Regex.IsMatch(parts[0], "^[0-9a-fA-F]{64}$"))
                return parts[0].ToLowerInvariant();
        }
        return null;
    }

    /// <summary>Plain file names only: no separators, traversal or control characters.</summary>
    public static bool SafeAssetName(string name) =>
        !string.IsNullOrWhiteSpace(name) && name.Length <= 200
        && name == Path.GetFileName(name)
        && !name.Contains("..", StringComparison.Ordinal)
        && !name.Contains('/') && !name.Contains('\\')
        && !name.Any(char.IsControl);
}

/// <summary>How the running build relates to the newest published Windows release.</summary>
public enum WindowsUpdateState
{
    UpToDate,
    UpdateAvailable,
    NewerThanRelease,
    Unknown,
}

public static class WindowsUpdateDecision
{
    /// <summary>Source revision recorded in this build's informational version ("1.0.0+&lt;sha&gt;"), or null.</summary>
    public static string? SourceRevision(string? informationalVersion)
    {
        if (string.IsNullOrWhiteSpace(informationalVersion)) return null;
        var separator = informationalVersion.IndexOf('+');
        if (separator < 0 || separator == informationalVersion.Length - 1) return null;
        var revision = informationalVersion[(separator + 1)..].Trim();
        return revision.Length == 0 ? null : revision;
    }

    /// <summary>Interpret the GitHub compare status between the build's source revision and the release tag.
    /// "ahead" means the tag is ahead of this build: an update exists. Anything unrecognized is Unknown,
    /// never an assumed answer.</summary>
    public static WindowsUpdateState FromCompareStatus(string? status) => status switch
    {
        "identical" => WindowsUpdateState.UpToDate,
        "ahead" => WindowsUpdateState.UpdateAvailable,
        "behind" => WindowsUpdateState.NewerThanRelease,
        _ => WindowsUpdateState.Unknown,
    };
}
