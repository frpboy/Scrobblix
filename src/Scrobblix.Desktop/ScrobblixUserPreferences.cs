using System.Text.Json;

namespace Scrobblix.Desktop;

/// <summary>
/// Only non-secret UI preferences are persisted here. Provider keys and session credentials
/// remain in Windows Credential Locker, not in this JSON file.
/// </summary>
internal sealed record ScrobblixUserPreferences(bool DesktopNotificationsEnabled)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public static async Task<ScrobblixUserPreferences> LoadAsync(string dataDirectory, CancellationToken cancellationToken)
    {
        var path = Path.Combine(dataDirectory, "preferences.json");
        if (!File.Exists(path))
        {
            return new ScrobblixUserPreferences(false);
        }

        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
            8192, FileOptions.Asynchronous | FileOptions.SequentialScan);
        return await JsonSerializer.DeserializeAsync<ScrobblixUserPreferences>(stream, JsonOptions, cancellationToken)
            ?? new ScrobblixUserPreferences(false);
    }

    public static async Task SaveAsync(
        string dataDirectory,
        ScrobblixUserPreferences preferences,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        Directory.CreateDirectory(dataDirectory);
        var path = Path.Combine(dataDirectory, "preferences.json");
        var temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await using (var stream = new FileStream(
                temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                8192, FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                await JsonSerializer.SerializeAsync(stream, preferences, JsonOptions, cancellationToken);
                await stream.FlushAsync(cancellationToken);
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            try { if (File.Exists(temporaryPath)) File.Delete(temporaryPath); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
