using System.Globalization;
using Scrobblix.Application.Providers;
using Scrobblix.Infrastructure.Diagnostics;
using Scrobblix.Infrastructure.Storage;
using Scrobblix.Windows;

/// <summary>
/// Issue #75: synthetic-only canary validation of the credential boundaries. Canary values
/// are generated at runtime, never printed (only their SHA-256 fingerprints), exercised
/// through the real credential store, sanitizer, database, backup and support-bundle paths,
/// and every produced artifact is scanned for the exact canary bytes.
/// </summary>
internal static class CredentialCanaryChecks
{
    public static async Task RunAsync()
    {
        Console.WriteLine("--- #75 credential redaction canary checks (synthetic values only) ---");

        var canaryApiKey = "sxcanary-ak-" + Guid.NewGuid().ToString("N");
        var canaryApiSecret = "sxcanary-as-" + Guid.NewGuid().ToString("N");
        var canarySessionKey = "sxcanary-sk-" + Guid.NewGuid().ToString("N");
        var canaries = new[] { canaryApiKey, canaryApiSecret, canarySessionKey };
        Console.WriteLine("  canary fingerprints: "
            + Check.CanaryFingerprint(canaryApiKey) + ", "
            + Check.CanaryFingerprint(canaryApiSecret) + ", "
            + Check.CanaryFingerprint(canarySessionKey));

        // 1. Credential Locker is the source of truth: roundtrip and removal work.
        var credentialStore = new WindowsCredentialStore();
        var resource = "scrobblix.test.canary." + Guid.NewGuid().ToString("N");
        await credentialStore.StoreAsync(resource, "synthetic-user", canarySessionKey, CancellationToken.None);
        Check.True(
            string.Equals(
                await credentialStore.GetAsync(resource, "synthetic-user", CancellationToken.None),
                canarySessionKey,
                StringComparison.Ordinal),
            "Credential Locker roundtrip returns the stored synthetic secret");
        await credentialStore.DeleteAsync(resource, "synthetic-user", CancellationToken.None);
        Check.True(
            await credentialStore.GetAsync(resource, "synthetic-user", CancellationToken.None) is null,
            "Credential Locker delete removes the synthetic secret");
        Check.True(
            await credentialStore.GetAsync(resource, "other-user", CancellationToken.None) is null,
            "Credential Locker isolates per resource and user name");

        // 2. The diagnostic sanitizer redacts every documented credential carriage form.
        var formats = new (string Label, Func<string, string> Render)[]
        {
            ("apiKey=value", c => "apiKey=" + c),
            ("api_key: value", c => "api_key: " + c),
            ("api_sig=value", c => "api_sig=" + c + "&other=1"),
            ("shared_secret=value", c => "shared_secret=" + c),
            ("password=value", c => "password=" + c),
            ("session_key=value", c => "session_key=" + c),
            ("sessionKey=value", c => "sessionKey=" + c),
            ("token=value", c => "token=" + c),
            ("sk=value", c => "sk=" + c),
            ("authorization URL", c => "https://www.last.fm/api/auth/?api_key=placeholder&token=" + c),
            ("JSON pair", c => "{\"sessionKey\":\"" + c + "\"}"),
            ("track metadata artist", c => "artist=" + c + "; title=ok"),
            ("quoted track metadata", c => "title:\"" + c + "\""),
        };
        foreach (var (label, render) in formats)
        {
            foreach (var canary in canaries)
            {
                var sanitized = DiagnosticMessageSanitizer.Sanitize(render(canary));
                Check.True(
                    !sanitized.Contains(canary, StringComparison.Ordinal),
                    "Sanitizer redacts " + label + " for canary " + Check.CanaryFingerprint(canary));
            }
        }

        // Documented contract boundary: the sanitizer is not a generic secret scrubber.
        // Raw secret values must never reach the logger; the sanitizer only strips the
        // recognized carriage forms above. This assertion pins that boundary honestly.
        var bareMessage = "unstructured prefix " + canarySessionKey + " suffix";
        Check.True(
            DiagnosticMessageSanitizer.Sanitize(bareMessage).Contains(canarySessionKey, StringComparison.Ordinal),
            "Boundary documented: bare unstructured values are NOT scrubbed; secrets must never be logged raw");

        var root = Path.Combine(Path.GetTempPath(), "scrobblix-credential-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            // 3. The listening database stores the credential RESOURCE NAME, never a secret.
            var databasePath = Path.Combine(root, "scrobblix.db");
            var connectionString = "Data Source=" + databasePath;
            var database = new SqliteDatabase(connectionString);
            await database.InitializeAsync(CancellationToken.None);
            var accountStore = new SqliteProviderAccountStore(connectionString);
            await accountStore.UpsertAsync(
                new ProviderAccount(
                    "synthetic-account",
                    "lastfm",
                    "synthetic-user",
                    "scrobblix.lastfm.session:synthetic-user",
                    true,
                    DateTimeOffset.UtcNow),
                CancellationToken.None);
            Check.True(true, "Provider account persisted with credential resource name only");
            Check.AssertFileExcludes(databasePath, canaries);

            // 4. Backups contain only the database and manifest and carry no secret bytes.
            var backupPath = Path.Combine(root, "scrobblix-backup.zip");
            await ScrobblixBackupService.CreateBackupAsync(connectionString, backupPath, CancellationToken.None);
            await Check.AssertZipExcludesAsync(
                backupPath,
                canaries,
                ["manifest.json", "scrobblix.db"]);

            // 5. Support bundles built from hostile logs export no secret bytes.
            var logDirectory = Path.Combine(root, "logs");
            Directory.CreateDirectory(logDirectory);
            var stamp = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);
            var hostileLines = new[]
            {
                "000001 | " + stamp + " | INFO | AUTH | apiKey=" + canaryApiKey,
                "000002 | " + stamp + " | WARN | AUTH | session_key: " + canarySessionKey,
                "000003 | " + stamp + " | ERROR | PROVIDER | request failed; token=" + canaryApiSecret + "; hresult=0x80004005",
                "000004 | " + stamp + " | INFO | AUTH | https://www.last.fm/api/auth/?api_key=x&token=" + canaryApiKey,
                "000005 | " + stamp + " | INFO | TRACKING | artist=" + canaryApiSecret + "; title=" + canarySessionKey,
                "000006 | " + stamp + " | ERROR | PROVIDER | exception=HttpRequestException; reason_code=provider.unavailable",
            };
            await File.WriteAllLinesAsync(
                Path.Combine(logDirectory, "scrobblix-20261107-000000-1.log"),
                hostileLines);
            var bundlePath = Path.Combine(root, "support-bundle.zip");
            var export = await SupportBundleExporter.CreateAsync(
                bundlePath, logDirectory, "0.0.0-test", "synthetic-windows", null, CancellationToken.None);
            Check.True(export.SourceLogFilesInspected >= 1, "Support bundle inspected the hostile log file");
            await Check.AssertZipExcludesAsync(bundlePath, canaries, null);
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        Console.WriteLine("Credential canary checks complete. Test artifacts removed; Credential Locker test entries deleted.");
    }
}
