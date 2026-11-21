using System.Runtime.InteropServices;
using System.Text.Json;
using Scrobblix.Application.Updates;
using Scrobblix.Domain.Updates;

namespace Scrobblix.Desktop;

/// <summary>
/// Guided update check against the official GitHub Releases channel.
/// Manual checks only: nothing runs on a timer, nothing installs silently, no signing change.
/// Download staging, hashing and custody live in Scrobblix.Application.Updates.WindowsUpdateDownload.
/// </summary>
public sealed class UpdateCheckService : IDisposable
{
    public sealed class UpdateCheckException : Exception
    {
        public UpdateCheckException(string diagnostic) : base(diagnostic) { }
    }

    public sealed record InstallerDownload(WindowsRelease Release, WindowsReleaseAsset Installer, string StagedPath, string? DownloadsPath, string Sha256);

    private const string ReleasesApi = "https://api.github.com/repos/frpboy/Scrobblix/releases?per_page=20";
    private const string CompareApiPrefix = "https://api.github.com/repos/frpboy/Scrobblix/compare/";
    private const int MaxApiBytes = 1024 * 1024;

    private readonly WindowsUpdateDownload _download = new();

    public void Dispose()
    {
        _download.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>Newest non-draft windows-v* release with the exact installer asset, or null.</summary>
    public async Task<WindowsRelease?> FindLatestAsync(CancellationToken cancellation)
    {
        byte[] body;
        try
        {
            body = await _download.GetBytesAsync(ReleasesApi, MaxApiBytes, cancellation);
        }
        catch (WindowsUpdateDownload.UpdateDownloadException failure)
        {
            throw new UpdateCheckException(failure.Message);
        }
        using var document = JsonDocument.Parse(body);
        if (document.RootElement.ValueKind != JsonValueKind.Array) throw new UpdateCheckException("Update information was not in the expected format.");
        var releases = new List<WindowsRelease>();
        foreach (var entry in document.RootElement.EnumerateArray())
        {
            var tag = entry.TryGetProperty("tag_name", out var tagValue) ? tagValue.GetString() : null;
            if (string.IsNullOrWhiteSpace(tag)) continue;
            var assets = new List<WindowsReleaseAsset>();
            if (entry.TryGetProperty("assets", out var assetList) && assetList.ValueKind == JsonValueKind.Array)
            {
                foreach (var asset in assetList.EnumerateArray())
                {
                    var name = asset.TryGetProperty("name", out var nameValue) ? nameValue.GetString() : null;
                    var url = asset.TryGetProperty("browser_download_url", out var urlValue) ? urlValue.GetString() : null;
                    if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(url)) continue;
                    if (!WindowsUpdateCatalog.SafeAssetName(name)) continue;
                    assets.Add(new WindowsReleaseAsset(name, url,
                        asset.TryGetProperty("size", out var sizeValue) && sizeValue.TryGetInt64(out var size) ? size : 0));
                }
            }
            releases.Add(new WindowsRelease(
                tag,
                entry.TryGetProperty("name", out var titleValue) ? titleValue.GetString() ?? tag : tag,
                entry.TryGetProperty("prerelease", out var prereleaseValue) && prereleaseValue.GetBoolean(),
                entry.TryGetProperty("draft", out var draftValue) && draftValue.GetBoolean(),
                entry.TryGetProperty("body", out var bodyValue) ? bodyValue.GetString() ?? "" : "",
                assets));
        }
        return WindowsUpdateCatalog.SelectLatest(releases);
    }

    /// <summary>GitHub compare between this build's source revision and the release tag; Unknown when it cannot be established.</summary>
    public async Task<WindowsUpdateState> CompareAsync(string sourceRevision, string tag, CancellationToken cancellation)
    {
        byte[] body;
        try
        {
            body = await _download.GetBytesAsync(CompareApiPrefix + Uri.EscapeDataString(sourceRevision) + "..." + Uri.EscapeDataString(tag), MaxApiBytes, cancellation);
        }
        catch (WindowsUpdateDownload.UpdateDownloadException failure)
        {
            throw new UpdateCheckException(failure.Message);
        }
        using var document = JsonDocument.Parse(body);
        var status = document.RootElement.TryGetProperty("status", out var statusValue) ? statusValue.GetString() : null;
        return WindowsUpdateDecision.FromCompareStatus(status);
    }

    /// <summary>Stage, hash-verify and copy the installer; nothing is executed.</summary>
    public async Task<InstallerDownload> DownloadInstallerAsync(WindowsRelease release, IProgress<(long Done, long? Total)>? progress, CancellationToken cancellation)
    {
        var staging = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Scrobblix", "updates");
        try
        {
            var result = await _download.DownloadAsync(release, staging, DownloadsFolder(), progress, cancellation);
            return new InstallerDownload(release, result.Installer, result.StagedPath, result.DownloadsPath, result.Sha256);
        }
        catch (WindowsUpdateDownload.UpdateDownloadException failure)
        {
            throw new UpdateCheckException(failure.Message);
        }
    }

    /// <summary>Re-hash the staged copy immediately before launching; a changed file is refused.</summary>
    public bool OpenInstallerVerified(string stagedPath, string expectedSha256) =>
        _download.OpenInstallerVerified(stagedPath, expectedSha256);

    /// <summary>The real per-user Downloads folder (redirect-aware); falls back to the profile folder.</summary>
    private static string DownloadsFolder()
    {
        if (OperatingSystem.IsWindows())
        {
            try
            {
                if (SHGetKnownFolderPath(FolderDownloads, 0, IntPtr.Zero, out var path) == 0 && path != IntPtr.Zero)
                {
                    var folder = Marshal.PtrToStringUni(path);
                    Marshal.FreeCoTaskMem(path);
                    if (!string.IsNullOrWhiteSpace(folder)) return folder;
                }
            }
            catch
            {
                // fall through to the profile folder
            }
        }
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
    }

    private static readonly Guid FolderDownloads = new("374DE290-123F-4565-9164-39C4925E467B");

    [DllImport("shell32.dll")]
    private static extern int SHGetKnownFolderPath([MarshalAs(UnmanagedType.LPStruct)] Guid id, uint flags, IntPtr token, out IntPtr path);
}
