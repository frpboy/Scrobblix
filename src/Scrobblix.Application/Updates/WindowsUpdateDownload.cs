using System.Net.Http;
using System.Security.Cryptography;
using Scrobblix.Domain.Updates;

namespace Scrobblix.Application.Updates;

/// <summary>
/// Guided installer download with staged custody: the file is downloaded to a unique private
/// staging directory, hashed while streaming, verified against SHA256SUMS, then copied to a
/// unique destination in the user's Downloads folder. A pre-existing user file is never
/// overwritten or deleted. The verified staged copy stays in app custody; the installer is
/// launched only after re-hashing it immediately before handoff.
/// The message handler and the launcher are injectable so tests use synthetic HTTP and a fake
/// launcher; no real network or installer execution happens in tests.
/// </summary>
public sealed class WindowsUpdateDownload
{
    public sealed class UpdateDownloadException : Exception
    {
        public UpdateDownloadException(string diagnostic) : base(diagnostic) { }
    }

    public sealed record Result(WindowsReleaseAsset Installer, string StagedPath, string? DownloadsPath, string Sha256);

    public const long MaxInstallerBytes = 512L * 1024 * 1024;
    public const int MaxChecksumsBytes = 256 * 1024;
    private const int MaxRedirects = 4;

    private readonly HttpClient _client;
    private readonly Func<string, bool> _launcher;

    public WindowsUpdateDownload(HttpMessageHandler? handler = null, Func<string, bool>? launcher = null)
    {
        _client = new HttpClient(handler ?? new HttpClientHandler { AllowAutoRedirect = false });
        _launcher = launcher ?? (path =>
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
        });
    }

    public static bool AllowedHost(string host) =>
        host is "api.github.com" or "github.com" or "codeload.github.com"
        || host.EndsWith(".githubusercontent.com", StringComparison.Ordinal);

    public static bool AllowedUrl(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps
        && string.IsNullOrEmpty(uri.UserInfo)
        && uri.IsDefaultPort
        && AllowedHost(uri.Host);

    private async Task<HttpResponseMessage> SendFollowingRedirectsAsync(string url, CancellationToken cancellation)
    {
        var current = url;
        for (var redirect = 0; ; redirect++)
        {
            if (!AllowedUrl(current)) throw new UpdateDownloadException("The update destination was not a recognized secure release address.");
            using var request = new HttpRequestMessage(HttpMethod.Get, current);
            request.Headers.UserAgent.ParseAdd("Scrobblix-Windows-UpdateCheck");
            var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellation);
            var status = (int)response.StatusCode;
            if (status is >= 300 and < 400)
            {
                var location = response.Headers.Location;
                response.Dispose();
                if (location is null) throw new UpdateDownloadException("The update service redirect was incomplete.");
                var resolved = new Uri(new Uri(current), location).AbsoluteUri;
                if (redirect >= MaxRedirects) throw new UpdateDownloadException("The update service redirected too many times.");
                if (!AllowedUrl(resolved)) throw new UpdateDownloadException("The update service redirected to an unrecognized destination.");
                current = resolved;
                continue;
            }
            return response;
        }
    }

    /// <summary>Bounded GET over the constrained redirect policy; used for the release and compare APIs.</summary>
    public async Task<byte[]> GetBytesAsync(string url, long maxBytes, CancellationToken cancellation)
    {
        using var response = await SendFollowingRedirectsAsync(url, cancellation);
        if (!response.IsSuccessStatusCode)
            throw new UpdateDownloadException($"Update information could not be retrieved (HTTP {(int)response.StatusCode}).");
        await using var stream = await response.Content.ReadAsStreamAsync(cancellation);
        using var output = new MemoryStream();
        var buffer = new byte[64 * 1024];
        while (true)
        {
            var read = await stream.ReadAsync(buffer, cancellation);
            if (read == 0) break;
            if (output.Length + read > maxBytes) throw new UpdateDownloadException("The update service response was larger than expected.");
            output.Write(buffer, 0, read);
        }
        return output.ToArray();
    }

    private async Task<string> ExpectedChecksumAsync(WindowsRelease release, CancellationToken cancellation)
    {
        var checksumsAsset = release.Assets.FirstOrDefault(asset => asset.Name == WindowsUpdateCatalog.ChecksumsName)
            ?? throw new UpdateDownloadException("This release does not publish integrity information; the download was not attempted.");
        using var response = await SendFollowingRedirectsAsync(checksumsAsset.DownloadUrl, cancellation);
        if (!response.IsSuccessStatusCode)
            throw new UpdateDownloadException("Integrity information for the update could not be retrieved.");
        var bytes = await response.Content.ReadAsByteArrayAsync(cancellation);
        if (bytes.Length > MaxChecksumsBytes) throw new UpdateDownloadException("Integrity information was larger than expected.");
        return WindowsUpdateCatalog.ChecksumFor(System.Text.Encoding.UTF8.GetString(bytes), WindowsUpdateCatalog.InstallerName)
            ?? throw new UpdateDownloadException("Integrity information for this exact installer is missing; the download was not attempted.");
    }

    /// <summary>Download, hash while streaming, verify, then copy to a unique Downloads destination.</summary>
    public async Task<Result> DownloadAsync(
        WindowsRelease release,
        string stagingRoot,
        string? downloadsDirectory,
        IProgress<(long Done, long? Total)>? progress,
        CancellationToken cancellation)
    {
        var matching = release.Assets.Where(asset => asset.Name == WindowsUpdateCatalog.InstallerName).ToList();
        if (matching.Count != 1)
            throw new UpdateDownloadException("This release does not contain exactly one recognized Windows installer; the download was not attempted.");
        var installer = matching[0];
        var expected = await ExpectedChecksumAsync(release, cancellation);

        var staging = Path.Combine(stagingRoot, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staging);
        var stagedPath = Path.Combine(staging, installer.Name);
        string? downloadsPath = null;
        try
        {
            using (var response = await SendFollowingRedirectsAsync(installer.DownloadUrl, cancellation))
            {
                if (!response.IsSuccessStatusCode)
                    throw new UpdateDownloadException($"The installer could not be downloaded (HTTP {(int)response.StatusCode}).");
                long? total = response.Content.Headers.ContentLength;
                await using var stream = await response.Content.ReadAsStreamAsync(cancellation);
                await using var file = new FileStream(stagedPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                using var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                var buffer = new byte[256 * 1024];
                long done = 0;
                while (true)
                {
                    var read = await stream.ReadAsync(buffer, cancellation);
                    if (read == 0) break;
                    done += read;
                    if (done > MaxInstallerBytes) throw new UpdateDownloadException("The installer is larger than expected.");
                    hasher.AppendData(buffer, 0, read);
                    await file.WriteAsync(buffer.AsMemory(0, read), cancellation);
                    progress?.Report((done, total));
                }
                var actual = Convert.ToHexString(hasher.GetHashAndReset()).ToLowerInvariant();
                if (actual != expected)
                    throw new UpdateDownloadException("The downloaded installer failed integrity verification and was deleted.");
            }

            if (downloadsDirectory is not null)
            {
                if (!WindowsUpdateCatalog.SafeAssetName(installer.Name))
                    throw new UpdateDownloadException("The installer name is not safe to store.");
                Directory.CreateDirectory(downloadsDirectory);
                downloadsPath = UniqueDestination(downloadsDirectory, installer.Name);
                File.Copy(stagedPath, downloadsPath);
            }
            return new Result(installer, stagedPath, downloadsPath, expected);
        }
        catch
        {
            // Only files this download created are removed; a pre-existing user file is never touched.
            try { if (File.Exists(stagedPath)) File.Delete(stagedPath); } catch { /* best effort */ }
            try { if (downloadsPath is not null && File.Exists(downloadsPath)) File.Delete(downloadsPath); } catch { /* best effort */ }
            try { if (Directory.Exists(staging)) Directory.Delete(staging); } catch { /* best effort */ }
            throw;
        }
    }

    private static string UniqueDestination(string directory, string name)
    {
        var stem = Path.GetFileNameWithoutExtension(name);
        var extension = Path.GetExtension(name);
        for (var attempt = 0; ; attempt++)
        {
            var candidate = Path.Combine(directory, attempt == 0 ? name : $"{stem} ({attempt}){extension}");
            if (!File.Exists(candidate)) return candidate;
            if (attempt > 100) throw new UpdateDownloadException("Could not find a free file name in the Downloads folder.");
        }
    }

    /// <summary>Launch only after re-hashing the staged copy immediately before handoff.
    /// A file that changed since verification is refused.</summary>
    public bool OpenInstallerVerified(string stagedPath, string expectedSha256)
    {
        try
        {
            if (HashFile(stagedPath) != expectedSha256) return false;
            return _launcher(stagedPath);
        }
        catch
        {
            return false;
        }
    }

    public static string HashFile(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }
}
