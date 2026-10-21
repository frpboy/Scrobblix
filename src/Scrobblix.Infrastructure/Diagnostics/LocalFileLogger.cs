using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Scrobblix.Application.Abstractions;

namespace Scrobblix.Infrastructure.Diagnostics;

/// <summary>
/// User-readable, structured local logs. Credentials and authorization material
/// are intentionally excluded from fields and sanitized if accidentally included.
/// </summary>
public sealed partial class LocalFileLogger : IEventLogger, IAsyncDisposable
{
    private const long MaximumFileBytes = 5 * 1024 * 1024;
    private const int MaximumRetainedFiles = 20;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly string _directory;
    private DateOnly? _activeDate;
    private string? _activePath;
    private long _entryNumber;
    private bool _initialized;

    public LocalFileLogger(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        _directory = Path.GetFullPath(directory);
        Directory.CreateDirectory(_directory);
    }

    public string DirectoryPath => _directory;

    public async Task WriteAsync(
        AppLogLevel level,
        string component,
        string eventName,
        string? correlationId = null,
        IReadOnlyDictionary<string, string?>? fields = null,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var now = DateTimeOffset.UtcNow;
            await EnsureActiveFileAsync(DateOnly.FromDateTime(now.UtcDateTime), cancellationToken);
            if (_activePath is null)
            {
                return;
            }

            var line = BuildEntry(
                ++_entryNumber,
                now,
                level,
                component,
                eventName,
                correlationId,
                fields);
            var byteCount = Encoding.UTF8.GetByteCount(line) + Environment.NewLine.Length;
            var length = File.Exists(_activePath) ? new FileInfo(_activePath).Length : 0;
            if (length + byteCount > MaximumFileBytes && length > 0)
            {
                RotateActiveFile();
                _entryNumber++;
                line = BuildEntry(_entryNumber, now, level, component, "LogRotated", correlationId,
                    new Dictionary<string, string?> { ["previousFile"] = Path.GetFileName(_activePath) });
                byteCount = Encoding.UTF8.GetByteCount(line) + Environment.NewLine.Length;
            }

            await using var stream = new FileStream(
                _activePath,
                FileMode.Append,
                FileAccess.Write,
                FileShare.ReadWrite,
                16 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            await using var writer = new StreamWriter(stream, new UTF8Encoding(false));
            await writer.WriteLineAsync(line.AsMemory(), cancellationToken);
            await writer.FlushAsync(cancellationToken);
            TrimOldFiles();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            // Diagnostics must never crash playback tracking when the disk is unavailable.
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task EnsureActiveFileAsync(DateOnly date, CancellationToken cancellationToken)
    {
        if (_initialized && _activeDate == date && _activePath is not null)
        {
            return;
        }

        _activeDate = date;
        _activePath = Path.Combine(_directory, $"scrobblix-{date:yyyy-MM-dd}.log");
        _entryNumber = 0;
        if (File.Exists(_activePath))
        {
            foreach (var line in await File.ReadAllLinesAsync(_activePath, cancellationToken))
            {
                if (TryReadEntryNumber(line, out var number))
                {
                    _entryNumber = Math.Max(_entryNumber, number);
                }
            }
        }
        _initialized = true;
    }

    private void RotateActiveFile()
    {
        if (_activePath is null || !File.Exists(_activePath))
        {
            return;
        }

        var stem = Path.GetFileNameWithoutExtension(_activePath);
        var extension = Path.GetExtension(_activePath);
        var index = 1;
        string target;
        do
        {
            target = Path.Combine(_directory, $"{stem}.{index}{extension}");
            index++;
        } while (File.Exists(target));

        File.Move(_activePath, target);
        using (File.Create(_activePath)) { }
    }

    private void TrimOldFiles()
    {
        var files = new DirectoryInfo(_directory)
            .GetFiles("scrobblix-*.log", SearchOption.TopDirectoryOnly)
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .ToArray();
        foreach (var oldFile in files.Skip(MaximumRetainedFiles))
        {
            try { oldFile.Delete(); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    private static string BuildEntry(
        long entryNumber,
        DateTimeOffset timestamp,
        AppLogLevel level,
        string component,
        string eventName,
        string? correlationId,
        IReadOnlyDictionary<string, string?>? fields)
    {
        var details = fields is null
            ? "{}"
            : JsonSerializer.Serialize(fields
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .ToDictionary(pair => Safe(pair.Key), pair => Safe(pair.Value)));
        return $"[{entryNumber:D6}] {timestamp.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture)} {level.ToString().ToUpperInvariant()} component={Safe(component)} event={Safe(eventName)} correlation={Safe(correlationId ?? "-")} details={details}";
    }

    private static bool TryReadEntryNumber(string line, out long number)
    {
        number = 0;
        var end = line.IndexOf(']');
        return line.Length > 2
            && line[0] == '['
            && end > 1
            && long.TryParse(line.AsSpan(1, end - 1), NumberStyles.None, CultureInfo.InvariantCulture, out number);
    }

    private static string? Safe(string? value)
    {
        if (value is null)
        {
            return null;
        }

        var flattened = value.Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' ');
        flattened = SensitiveParameterRegex().Replace(flattened, "$1=[REDACTED]");
        return flattened.Length > 512 ? flattened[..512] + "…" : flattened;
    }

    [GeneratedRegex("(?i)(api[_-]?key|api[_-]?sig|shared[_-]?secret|password|session[_-]?key|token|\\bsk)\\s*[=:]\\s*[^&\\s,;]+")]
    private static partial Regex SensitiveParameterRegex();

    public ValueTask DisposeAsync()
    {
        _gate.Dispose();
        return ValueTask.CompletedTask;
    }
}
