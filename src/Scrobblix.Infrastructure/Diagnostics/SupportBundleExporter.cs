using System.Globalization;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Scrobblix.Infrastructure.Diagnostics;

public sealed record SupportBundleExportResult(
    string ArchivePath,
    string Sha256,
    int SourceLogFilesInspected,
    int OperationalEventsIncluded);

/// <summary>
/// Builds a diagnostics-only ZIP. It never opens the listening database, preferences,
/// Credential Locker, artwork folder, or raw crash dumps. Raw log messages are parsed
/// only to extract a strict allow-list of operational fields; they are never exported.
/// </summary>
public static class SupportBundleExporter
{
    private const int MaximumLogFiles = 5;
    private const int MaximumEventsPerFile = 750;
    private const long MaximumBytesPerLogFile = 8 * 1024 * 1024;
    private static readonly Regex SafeCategory = new("^[A-Z0-9_]{1,32}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex ExceptionTypePattern = new(@"(?i)\bexception\s*=\s*([A-Za-z_][A-Za-z0-9_.+]{0,100})", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex HResultPattern = new(@"(?i)\bhresult\s*=\s*(-?(?:0x)?[0-9a-f]{1,16})", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex ReasonCodePattern = new(@"(?i)\breason(?:_code)?\s*=\s*([a-z][a-z0-9_.-]{0,79})", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static async Task<SupportBundleExportResult> CreateAsync(
        string destinationPath,
        string logDirectory,
        string applicationVersion,
        string windowsVersion,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(logDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(windowsVersion);

        var archivePath = Path.GetFullPath(destinationPath);
        var destinationDirectory = Path.GetDirectoryName(archivePath);
        if (string.IsNullOrWhiteSpace(destinationDirectory))
        {
            throw new ArgumentException("The destination must include a directory.", nameof(destinationPath));
        }

        Directory.CreateDirectory(destinationDirectory);
        var tempPath = Path.Combine(destinationDirectory, $".scrobblix-support-{Guid.NewGuid():N}.tmp");

        try
        {
            var snapshot = await Task.Run(
                () => Collect(logDirectory, applicationVersion, windowsVersion, cancellationToken),
                cancellationToken).ConfigureAwait(false);

            await using (var stream = new FileStream(
                tempPath,
                FileMode.CreateNew,
                FileAccess.ReadWrite,
                FileShare.None,
                bufferSize: 81920,
                useAsync: true))
            {
                using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
                {
                    await AddEntryAsync(archive, "manifest.json", snapshot.ManifestJson, cancellationToken).ConfigureAwait(false);
                    await AddEntryAsync(archive, "event-summary.txt", snapshot.SummaryText, cancellationToken).ConfigureAwait(false);
                    await AddEntryAsync(archive, "operational-events.tsv", snapshot.EventsTsv, cancellationToken).ConfigureAwait(false);
                    await AddEntryAsync(archive, "README.txt", ReadmeText, cancellationToken).ConfigureAwait(false);
                }

                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            File.Move(tempPath, archivePath, overwrite: true);
            await using var resultStream = new FileStream(archivePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
            var digest = await SHA256.HashDataAsync(resultStream, cancellationToken).ConfigureAwait(false);
            return new SupportBundleExportResult(
                archivePath,
                Convert.ToHexString(digest).ToLowerInvariant(),
                snapshot.SourceLogFilesInspected,
                snapshot.Events.Count);
        }
        finally
        {
            try
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    private static BundleSnapshot Collect(
        string logDirectory,
        string applicationVersion,
        string windowsVersion,
        CancellationToken cancellationToken)
    {
        var events = new List<OperationalEvent>();
        var inspected = 0;

        if (Directory.Exists(logDirectory))
        {
            FileInfo[] candidates;
            try
            {
                candidates = new DirectoryInfo(logDirectory)
                    .GetFiles("scrobblix-*.log", SearchOption.TopDirectoryOnly)
                    .Where(file => file.Length <= MaximumBytesPerLogFile)
                    .OrderByDescending(file => file.LastWriteTimeUtc)
                    .Take(MaximumLogFiles)
                    .ToArray();
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                candidates = [];
            }

            foreach (var file in candidates)
            {
                cancellationToken.ThrowIfCancellationRequested();
                inspected++;
                try
                {
                    var tail = new Queue<string>();
                    foreach (var line in File.ReadLines(file.FullName))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        tail.Enqueue(line);
                        while (tail.Count > MaximumEventsPerFile)
                        {
                            tail.Dequeue();
                        }
                    }

                    foreach (var line in tail)
                    {
                        if (TryParseOperationalEvent(line, out var operationalEvent))
                        {
                            events.Add(operationalEvent);
                        }
                    }
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    // Keep the bundle usable when a log was rotated or locked during collection.
                }
            }
        }

        events = events
            .OrderBy(item => item.TimestampUtc)
            .TakeLast(MaximumLogFiles * MaximumEventsPerFile)
            .ToList();

        var safeAppVersion = Regex.IsMatch(applicationVersion, @"^[A-Za-z0-9.+_-]{1,80}$", RegexOptions.CultureInvariant)
            ? applicationVersion
            : "unknown";
        var safeWindowsVersion = Regex.IsMatch(windowsVersion, @"^[A-Za-z0-9.+_-]{1,80}$", RegexOptions.CultureInvariant)
            ? windowsVersion
            : "unknown";
        var manifest = new
        {
            formatVersion = 1,
            generatedAtUtc = DateTimeOffset.UtcNow,
            applicationVersion = safeAppVersion,
            windowsVersion = safeWindowsVersion,
            framework = RuntimeInformation.FrameworkDescription,
            processArchitecture = RuntimeInformation.ProcessArchitecture.ToString(),
            logFilesInspected = inspected,
            operationalEventsIncluded = events.Count,
            includedFiles = new[] { "manifest.json", "event-summary.txt", "operational-events.tsv", "README.txt" },
            excludedByDesign = new[] { "listening database/history", "raw log messages", "artist/title/album", "artwork", "credentials/session keys", "full URLs", "user names", "full file paths", "raw crash dumps" },
        };

        var summary = new StringBuilder();
        summary.AppendLine("Scrobblix privacy-safe diagnostics summary");
        summary.AppendLine($"Generated (UTC): {DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture)}");
        summary.AppendLine($"Application version: {safeAppVersion}");
        summary.AppendLine($"Windows version: {safeWindowsVersion}");
        summary.AppendLine($"Log files inspected: {inspected.ToString(CultureInfo.InvariantCulture)}");
        summary.AppendLine($"Operational events included: {events.Count.ToString(CultureInfo.InvariantCulture)}");
        summary.AppendLine();
        summary.AppendLine("Counts by level and category (no original messages are included):");
        foreach (var group in events.GroupBy(item => (item.Level, item.Category)).OrderBy(group => group.Key.Level).ThenBy(group => group.Key.Category, StringComparer.Ordinal))
        {
            summary.AppendLine($"{group.Key.Level}\t{group.Key.Category}\t{group.Count().ToString(CultureInfo.InvariantCulture)}");
        }

        var eventLines = new StringBuilder("timestamp_utc\tlevel\tcategory\texception_type\thresult\treason_code\n");
        foreach (var item in events)
        {
            eventLines.Append(item.TimestampUtc.ToString("O", CultureInfo.InvariantCulture)).Append('\t')
                .Append(item.Level).Append('\t')
                .Append(item.Category).Append('\t')
                .Append(item.ExceptionType ?? string.Empty).Append('\t')
                .Append(item.HResult ?? string.Empty).Append('\t')
                .Append(item.ReasonCode ?? string.Empty).Append('\n');
        }

        return new BundleSnapshot(
            JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }),
            summary.ToString(),
            eventLines.ToString(),
            inspected,
            events);
    }

    private static bool TryParseOperationalEvent(string line, out OperationalEvent operationalEvent)
    {
        operationalEvent = default!;
        // Logger format: sequence | UTC timestamp | level | category | message.
        var fields = new string[5];
        var position = 0;
        for (var index = 0; index < 4; index++)
        {
            var separator = line.IndexOf('|', position);
            if (separator < 0)
            {
                return false;
            }

            fields[index] = line[position..separator].Trim();
            position = separator + 1;
        }

        fields[4] = line[position..].Trim();
        if (!DateTimeOffset.TryParse(fields[1], CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var timestamp))
        {
            return false;
        }

        var level = fields[2];
        if (level is not "INFO" and not "WARN" and not "ERROR")
        {
            return false;
        }

        var category = fields[3];
        if (!SafeCategory.IsMatch(category))
        {
            return false;
        }

        // Raw message is parsed only for tightly constrained diagnostic fields, never copied.
        var exception = ExceptionTypePattern.Match(fields[4]);
        var hresult = HResultPattern.Match(fields[4]);
        var reason = ReasonCodePattern.Match(fields[4]);
        operationalEvent = new OperationalEvent(
            timestamp,
            level,
            category,
            exception.Success ? exception.Groups[1].Value : null,
            hresult.Success ? hresult.Groups[1].Value : null,
            reason.Success ? reason.Groups[1].Value : null);
        return true;
    }

    private static async Task AddEntryAsync(ZipArchive archive, string name, string content, CancellationToken cancellationToken)
    {
        var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
        await using var stream = entry.Open();
        await using var writer = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: false);
        await writer.WriteAsync(content.AsMemory(), cancellationToken).ConfigureAwait(false);
    }

    private const string ReadmeText = """
Scrobblix privacy-safe support bundle

This bundle was created locally at the user's request. It includes version/runtime
metadata plus operational event timestamps, level, category and constrained
exception/HRESULT/reason codes. Original free-form log messages are never exported.

It does not include the listening database/history, artist/title/album metadata,
artwork, API credentials, session keys, full URLs, usernames, full local paths,
preferences, raw crash dumps, or log files themselves.

Review the included manifest.json and operational-events.tsv before sharing.
The bundle does not upload itself anywhere. The user chooses whether and where
to share it. Treat exception names and event timing as diagnostics information.
""";

    private sealed record OperationalEvent(
        DateTimeOffset TimestampUtc,
        string Level,
        string Category,
        string? ExceptionType,
        string? HResult,
        string? ReasonCode);

    private sealed record BundleSnapshot(
        string ManifestJson,
        string SummaryText,
        string EventsTsv,
        int SourceLogFilesInspected,
        List<OperationalEvent> Events);
}
