using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Scrobblix.Desktop;

/// <summary>
/// Append-only diagnostic log for the current Scrobblix process.
/// Each row has a monotonically increasing entry number and UTC timestamp.
/// API secrets and session keys must never be passed to this logger.
/// </summary>
public sealed class ScrobblixFileLogger
{
    private const long MaximumFileBytes = 8 * 1024 * 1024;
    private const int MaximumRetainedLogFiles = 20;
    private readonly object _gate = new();
    private long _entryNumber;
    private readonly string _logPath;

    public string LogPath => _logPath;

    public ScrobblixFileLogger(string dataDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);
        var logDirectory = Path.Combine(dataDirectory, "logs");
        Directory.CreateDirectory(logDirectory);
        var started = DateTimeOffset.Now;
        _logPath = Path.Combine(
            logDirectory,
            $"scrobblix-{started:yyyyMMdd-HHmmss}-{Environment.ProcessId}.log");
        Write("INFO", "LOGGER", "Started log session; entries use sequential numbers and UTC timestamps.");
    }

    public void Info(string category, string message) => Write("INFO", category, message);
    public void Warning(string category, string message) => Write("WARN", category, message);
    public void Error(string category, string message) => Write("ERROR", category, message);

    private void Write(string level, string category, string message)
    {
        lock (_gate)
        {
            var entry = Interlocked.Increment(ref _entryNumber);
            var timestamp = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);
            var safeCategory = Sanitize(category, 32);
            var safeMessage = Sanitize(message, 6000);
            var line = $"{entry:D6} | {timestamp} | {level} | {safeCategory} | {safeMessage}{Environment.NewLine}";
            try
            {
                RotateIfNeeded(Encoding.UTF8.GetByteCount(line));
                File.AppendAllText(_logPath, line, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                TrimOldLogs();
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                Debug.WriteLine($"Scrobblix log write failed: {exception.GetType().Name}");
            }
        }
    }

    private static string Sanitize(string? value, int maximumLength)
    {
        var normalized = (value ?? string.Empty)
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Replace('\t', ' ');
        normalized = Regex.Replace(normalized,
            @"(?i)(api[_-]?key|api[_-]?sig|shared[_-]?secret|password|session[_-]?key|token|sk)\s*[=:]\s*[^&\s,;]+",
            "$1=[REDACTED]");
        normalized = Regex.Replace(normalized, @"(?i)https://www\.last\.fm/api/auth/\\?[^\s]+", "[AUTHORIZATION_URL_REDACTED]");
        return normalized.Length <= maximumLength ? normalized : normalized[..maximumLength] + "…";
    }

    private void RotateIfNeeded(int incomingBytes)
    {
        if (!File.Exists(_logPath) || new FileInfo(_logPath).Length + incomingBytes <= MaximumFileBytes)
        {
            return;
        }

        var directory = Path.GetDirectoryName(_logPath)!;
        var stem = Path.GetFileNameWithoutExtension(_logPath);
        var index = 1;
        string rotatedPath;
        do
        {
            rotatedPath = Path.Combine(directory, $"{stem}.{index}.log");
            index++;
        } while (File.Exists(rotatedPath));

        File.Move(_logPath, rotatedPath);
    }

    private void TrimOldLogs()
    {
        var directory = Path.GetDirectoryName(_logPath)!;
        var files = new DirectoryInfo(directory)
            .GetFiles("scrobblix-*.log", SearchOption.TopDirectoryOnly)
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .ToArray();
        foreach (var file in files.Skip(MaximumRetainedLogFiles))
        {
            try { file.Delete(); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
