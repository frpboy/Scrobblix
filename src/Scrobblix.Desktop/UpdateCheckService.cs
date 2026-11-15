using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using Scrobblix.Domain.Updates;

namespace Scrobblix.Desktop;

/// <summary>
/// Guided update check against the official GitHub Releases channel.
/// Manual checks only: nothing runs on a timer, nothing installs silently, no signing change.
/// Transport policy matches the Android direct channel: HTTPS, official release hosts only,
/// bounded response sizes, redirects constrained to the same hosts.
/// </summary>
public sealed class UpdateCheckService
{
    public sealed class UpdateCheckException : Exception
    {
        public UpdateCheckException(string diagnostic) : base(diagnostic) { }
    }

    public sealed record InstallerDownload(WindowsRelease Release, WindowsReleaseAsset Installer, string Path, string Sha256);

    private const string ReleasesApi = "https://api.github.com/repos/frpboy/Scrobblix/releases?per_page=20";
    private const string CompareApiPrefix = "https://api.github.com/repos/frpboy/Scrobblix/compare/";
    private const int MaxReleasesBytes = 1024 * 1024;
    private const int MaxChecksumsBytes = 256 * 1024;
    private const long MaxInstallerBytes = 512L * 1024 * 1024;
    private const int MaxRedirects = 4;

    private static readonly HttpClient Client = new(new HttpClientHandler { AllowAutoRedirect = false });

    private static bool AllowedHost(string host) =>
        host is "api.github.com" or "github.com" or "codeload.github.com"
        || host.EndsWith(".githubusercontent.com", StringComparison.Ordinal);

    private static bool AllowedUrl(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps
        && string.IsNullOrEmpty(uri.UserInfo)
        && uri.IsDefaultPort
        && AllowedHost(uri.Host);

    private static HttpRequestMessage Request(string url)
    {
        if (!AllowedUrl(url)) throw new UpdateCheckException("The update destination was not a recognized secure release address.");
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.UserAgent.ParseAdd("Scrobblix-Windows-UpdateCheck");
        request.Headers.Accept.ParseAdd("application/vnd.github+json");
        return request;
    }

    private static async Task<HttpResponseMessage> SendFollowingRedirectsAsync(string url, CancellationToken cancellation)
    {
        var current = url;
        for (var redirect = 0; ; redirect++)
        {
            using var request = Request(current);
            var response = await Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellation);
            var status = (int)response.StatusCode;
            if (status is >= 300 and < 400)
            {
                var location = response.Headers.Location;
                response.Dispose();
                if (location is null) throw new UpdateCheckException("The update service redirect was incomplete.");
                var resolved = new Uri(new Uri(current), location).AbsoluteUri;
                if (redirect >= MaxRedirects) throw new UpdateCheckException("The update service redirected too many times.");
                if (!AllowedUrl(resolved)) throw new UpdateCheckException("The update service redirected to an unrecognized destination.");
                current = resolved;
                continue;
            }
            return response;
        }
    }

    private static async Task<byte[]> ReadBoundedAsync(HttpResponseMessage response, long maxBytes, CancellationToken cancellation)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellation);
        using var output = new MemoryStream();
        var buffer = new byte[64 * 1024];
        while (true)
        {
            var read = await stream.ReadAsync(buffer, cancellation);
            if (read == 0) break;
            if (output.Length + read > maxBytes) throw new UpdateCheckException("The update service response was larger than expected.");
            output.Write(buffer, 0, read);
        }
        return output.ToArray();
    }

    private static async Task<byte[]> GetBytesAsync(string url, long maxBytes, CancellationToken cancellation)
    {
        using var response = await SendFollowingRedirectsAsync(url, cancellation);
        if (!response.IsSuccessStatusCode)
            throw new UpdateCheckException($"Update information could not be retrieved (HTTP {(int)response.StatusCode}).");
        return await ReadBoundedAsync(response, maxBytes, cancellation);
    }

    /// <summary>Newest non-draft windows-v* release with an installer asset, or null.</summary>
    public static async Task<WindowsRelease?> FindLatestAsync(CancellationToken cancellation)
    {
        var body = await GetBytesAsync(ReleasesApi, MaxReleasesBytes, cancellation);
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
    public static async Task<WindowsUpdateState> CompareAsync(string sourceRevision, string tag, CancellationToken cancellation)
    {
        var body = await GetBytesAsync(CompareApiPrefix + Uri.EscapeDataString(sourceRevision) + "..." + Uri.EscapeDataString(tag), MaxReleasesBytes, cancellation);
        using var document = JsonDocument.Parse(body);
        var status = document.RootElement.TryGetProperty("status", out var statusValue) ? statusValue.GetString() : null;
        return WindowsUpdateDecision.FromCompareStatus(status);
    }

    /// <summary>Download the installer to the user's Downloads folder and verify it against SHA256SUMS.
    /// Any failure deletes the partial file. Nothing is executed.</summary>
    public static async Task<InstallerDownload> DownloadInstallerAsync(WindowsRelease release, IProgress<(long Done, long? Total)>? progress, CancellationToken cancellation)
    {
        var installer = release.Assets.FirstOrDefault(asset => asset.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            ?? throw new UpdateCheckException("This release does not include a Windows installer.");
        var checksumsAsset = release.Assets.FirstOrDefault(asset => asset.Name == WindowsUpdateCatalog.ChecksumsName)
            ?? throw new UpdateCheckException("This release does not publish integrity information; the download was not attempted.");
        var checksumsBody = await GetBytesAsync(checksumsAsset.DownloadUrl, MaxChecksumsBytes, cancellation);
        var expected = WindowsUpdateCatalog.ChecksumFor(System.Text.Encoding.UTF8.GetString(checksumsBody), installer.Name)
            ?? throw new UpdateCheckException("Integrity information for this exact installer is missing; the download was not attempted.");

        var downloads = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        Directory.CreateDirectory(downloads);
        var target = Path.Combine(downloads, installer.Name);
        try
        {
            using (var response = await SendFollowingRedirectsAsync(installer.DownloadUrl, cancellation))
            {
                if (!response.IsSuccessStatusCode)
                    throw new UpdateCheckException($"The installer could not be downloaded (HTTP {(int)response.StatusCode}).");
                long? total = response.Content.Headers.ContentLength;
                await using var stream = await response.Content.ReadAsStreamAsync(cancellation);
                await using var file = new FileStream(target, FileMode.Create, FileAccess.Write, FileShare.None);
                var buffer = new byte[256 * 1024];
                long done = 0;
                while (true)
                {
                    var read = await stream.ReadAsync(buffer, cancellation);
                    if (read == 0) break;
                    done += read;
                    if (done > MaxInstallerBytes) throw new UpdateCheckException("The installer is larger than expected.");
                    await file.WriteAsync(buffer.AsMemory(0, read), cancellation);
                    progress?.Report((done, total));
                }
            }
            var actual = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(target, cancellation))).ToLowerInvariant();
            if (actual != expected)
                throw new UpdateCheckException("The downloaded installer failed integrity verification and was deleted.");
            return new InstallerDownload(release, installer, target, actual);
        }
        catch
        {
            try { if (File.Exists(target)) File.Delete(target); } catch { /* best effort cleanup */ }
            throw;
        }
    }

    /// <summary>Launch the verified installer for the user to confirm in Windows. Returns false if it could not start.</summary>
    public static bool OpenInstaller(string path)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
            return true;
        }
        catch
        {
            return false;
        }
    }
}
