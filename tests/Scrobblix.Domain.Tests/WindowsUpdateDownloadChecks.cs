using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using Scrobblix.Application.Updates;
using Scrobblix.Domain.Updates;

/// <summary>
/// Synthetic-HTTP tests for the staged download custody flow. No real network: every response
/// comes from a fake handler. No real installer execution: the launcher is a recording fake.
/// </summary>
internal static class WindowsUpdateDownloadChecks
{
    private static void Assert(bool value, string message)
    {
        if (!value) throw new InvalidOperationException("windows-update-download-check: " + message);
    }

    private sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<string> Requests { get; } = new();
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri!.ToString());
            return Task.FromResult(respond(request));
        }
    }

    private static HttpResponseMessage Bytes(byte[] body) =>
        new(HttpStatusCode.OK) { Content = new ByteArrayContent(body) };

    private static HttpResponseMessage Redirect(string location) =>
        new(HttpStatusCode.Found) { Headers = { Location = new Uri(location) } };

    private static string Sha256Of(byte[] content) =>
        Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();

    private static WindowsRelease ReleaseFor(byte[] installerBytes, string installerUrl, string sumsUrl)
    {
        var sums = Sha256Of(installerBytes) + "  " + WindowsUpdateCatalog.InstallerName + "\n";
        return new WindowsRelease("windows-v0.1.0-beta.12", "beta 12", true, false, "notes",
            new[]
            {
                new WindowsReleaseAsset(WindowsUpdateCatalog.InstallerName, installerUrl, installerBytes.Length),
                new WindowsReleaseAsset(WindowsUpdateCatalog.ChecksumsName, sumsUrl, sums.Length),
            });
    }

    public static async Task RunAsync()
    {
        var installerBytes = Enumerable.Range(0, 700000).Select(i => (byte)(i % 251)).ToArray();
        var installerUrl = "https://github.com/frpboy/Scrobblix/releases/download/x/" + WindowsUpdateCatalog.InstallerName;
        var sumsUrl = "https://github.com/frpboy/Scrobblix/releases/download/x/SHA256SUMS.txt";

        // Success: staged, hash verified, copied to a unique Downloads destination.
        {
            var handler = new FakeHandler(request => Bytes(request.RequestUri!.ToString().Contains("SHA256SUMS")
                ? System.Text.Encoding.UTF8.GetBytes(Sha256Of(installerBytes) + "  " + WindowsUpdateCatalog.InstallerName + "\n")
                : installerBytes));
            using var download = new WindowsUpdateDownload(handler);
            var staging = Path.Combine(Path.GetTempPath(), "scrobblix-stage-" + Guid.NewGuid().ToString("N"));
            var downloads = Path.Combine(Path.GetTempPath(), "scrobblix-dl-" + Guid.NewGuid().ToString("N"));
            var result = await download.DownloadAsync(ReleaseFor(installerBytes, installerUrl, sumsUrl), staging, downloads, null, CancellationToken.None);
            Assert(File.Exists(result.StagedPath), "staged file exists");
            Assert(result.DownloadsPath is not null && File.Exists(result.DownloadsPath), "downloads copy exists");
            Assert(result.Sha256 == Sha256Of(installerBytes), "hash recorded");
            Assert(new FileInfo(result.StagedPath).Length == installerBytes.Length, "content length matches");
        }

        // Pre-existing user file is never overwritten or deleted: a unique destination is chosen.
        {
            var handler = new FakeHandler(request => Bytes(request.RequestUri!.ToString().Contains("SHA256SUMS")
                ? System.Text.Encoding.UTF8.GetBytes(Sha256Of(installerBytes) + "  " + WindowsUpdateCatalog.InstallerName + "\n")
                : installerBytes));
            using var download = new WindowsUpdateDownload(handler);
            var staging = Path.Combine(Path.GetTempPath(), "scrobblix-stage-" + Guid.NewGuid().ToString("N"));
            var downloads = Path.Combine(Path.GetTempPath(), "scrobblix-dl-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(downloads);
            var preexisting = Path.Combine(downloads, WindowsUpdateCatalog.InstallerName);
            await File.WriteAllBytesAsync(preexisting, new byte[] { 1, 2, 3 });
            var result = await download.DownloadAsync(ReleaseFor(installerBytes, installerUrl, sumsUrl), staging, downloads, null, CancellationToken.None);
            Assert(result.DownloadsPath != preexisting, "unique destination chosen over the pre-existing file");
            Assert((await File.ReadAllBytesAsync(preexisting)).SequenceEqual(new byte[] { 1, 2, 3 }), "pre-existing user file untouched");

            // Name collision chain: with base and " (1)" both present, the copy lands in " (2)" and both stay intact.
            var firstCopy = Path.Combine(downloads, Path.GetFileNameWithoutExtension(WindowsUpdateCatalog.InstallerName) + " (1).exe");
            await File.WriteAllBytesAsync(firstCopy, new byte[] { 4, 5, 6 });
            var second = await download.DownloadAsync(ReleaseFor(installerBytes, installerUrl, sumsUrl),
                Path.Combine(Path.GetTempPath(), "scrobblix-stage-" + Guid.NewGuid().ToString("N")), downloads, null, CancellationToken.None);
            Assert(second.DownloadsPath!.EndsWith(" (2).exe", StringComparison.Ordinal), "next free destination chosen");
            Assert((await File.ReadAllBytesAsync(firstCopy)).SequenceEqual(new byte[] { 4, 5, 6 }), "earlier copy untouched");
            Assert((await File.ReadAllBytesAsync(preexisting)).SequenceEqual(new byte[] { 1, 2, 3 }), "original still untouched");
        }

        // Hash mismatch: staged file deleted, nothing left behind, no launcher.
        {
            var handler = new FakeHandler(request => Bytes(request.RequestUri!.ToString().Contains("SHA256SUMS")
                ? System.Text.Encoding.UTF8.GetBytes(new string('0', 64) + "  " + WindowsUpdateCatalog.InstallerName + "\n")
                : installerBytes));
            using var download = new WindowsUpdateDownload(handler);
            var staging = Path.Combine(Path.GetTempPath(), "scrobblix-stage-" + Guid.NewGuid().ToString("N"));
            var threw = false;
            try { await download.DownloadAsync(ReleaseFor(installerBytes, installerUrl, sumsUrl), staging, null, null, CancellationToken.None); }
            catch (WindowsUpdateDownload.UpdateDownloadException) { threw = true; }
            Assert(threw, "hash mismatch rejected");
            Assert(!Directory.Exists(staging) || !Directory.GetFiles(staging, "*", SearchOption.AllDirectories).Any(), "mismatch leaves no files");
        }

        // Redirect policy: allowed host followed, foreign host blocked.
        {
            var cdn = "https://release-assets.githubusercontent.com/github-production-release-asset/x";
            var handler = new FakeHandler(request =>
                request.RequestUri!.ToString() == installerUrl ? Redirect(cdn)
                : request.RequestUri!.ToString() == cdn ? Bytes(installerBytes)
                : Bytes(System.Text.Encoding.UTF8.GetBytes(Sha256Of(installerBytes) + "  " + WindowsUpdateCatalog.InstallerName + "\n")));
            using var download = new WindowsUpdateDownload(handler);
            var staging = Path.Combine(Path.GetTempPath(), "scrobblix-stage-" + Guid.NewGuid().ToString("N"));
            var result = await download.DownloadAsync(ReleaseFor(installerBytes, installerUrl, sumsUrl), staging, null, null, CancellationToken.None);
            Assert(File.Exists(result.StagedPath), "allowed redirect followed");

            var evilHandler = new FakeHandler(request =>
                request.RequestUri!.ToString() == installerUrl ? Redirect("https://evil.example.com/setup.exe")
                : Bytes(System.Text.Encoding.UTF8.GetBytes(Sha256Of(installerBytes) + "  " + WindowsUpdateCatalog.InstallerName + "\n")));
            using var evilDownload = new WindowsUpdateDownload(evilHandler);
            var blocked = false;
            try { await evilDownload.DownloadAsync(ReleaseFor(installerBytes, installerUrl, sumsUrl), staging, null, null, CancellationToken.None); }
            catch (WindowsUpdateDownload.UpdateDownloadException) { blocked = true; }
            Assert(blocked, "foreign redirect blocked");
            Assert(evilHandler.Requests.All(url => !url.StartsWith("https://evil.example.com", StringComparison.Ordinal)), "foreign host never contacted");
        }

        // Launch custody: tampered staged file is refused; intact file reaches the fake launcher.
        {
            var handler = new FakeHandler(request => Bytes(request.RequestUri!.ToString().Contains("SHA256SUMS")
                ? System.Text.Encoding.UTF8.GetBytes(Sha256Of(installerBytes) + "  " + WindowsUpdateCatalog.InstallerName + "\n")
                : installerBytes));
            var launched = new List<string>();
            using var download = new WindowsUpdateDownload(handler, path => { launched.Add(path); return true; });
            var staging = Path.Combine(Path.GetTempPath(), "scrobblix-stage-" + Guid.NewGuid().ToString("N"));
            var result = await download.DownloadAsync(ReleaseFor(installerBytes, installerUrl, sumsUrl), staging, null, null, CancellationToken.None);
            Assert(download.OpenInstallerVerified(result.StagedPath, result.Sha256), "verified staged file launches");
            Assert(launched.Count == 1 && launched[0] == result.StagedPath, "launcher received the staged path");
            await File.WriteAllBytesAsync(result.StagedPath, new byte[] { 9, 9, 9 });
            Assert(!download.OpenInstallerVerified(result.StagedPath, result.Sha256), "tampered staged file refused");
            Assert(launched.Count == 1, "launcher not called for the tampered file");
        }

        Console.WriteLine("windows-update-download-check: PASS");
    }
}
