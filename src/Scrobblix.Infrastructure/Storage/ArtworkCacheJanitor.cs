using Microsoft.Data.Sqlite;

namespace Scrobblix.Infrastructure.Storage;

public readonly record struct ArtworkCachePruneResult(long Deleted, long Failed);

/// <summary>
/// Enforces the owner-approved 30-day artwork cache retention (issue #122, approved 2026-11-07).
/// Cached artwork files whose last write is older than the cutoff are deleted unless a retained
/// plays-history row still references the path. Only image files and orphaned temporary files
/// directly inside the artwork cache directory are ever removed; listening history, provider
/// queues and every other file or directory are left untouched. Locked or inaccessible files are
/// skipped and retried on the next cycle. Only file counts leave this type, never track metadata.
/// </summary>
public sealed class ArtworkCacheJanitor(string connectionString, string artworkDirectory)
{
    private const int MaximumBatchSize = 5_000;
    private static readonly HashSet<string> RemovableExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png",
        ".jpg",
        ".jpeg",
        ".webp",
        ".gif",
        ".bmp",
    };

    public async Task<ArtworkCachePruneResult> PruneExpiredAsync(
        DateTimeOffset cutoffUtc,
        DateTimeOffset temporaryCutoffUtc,
        int batchSize,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(batchSize, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(batchSize, MaximumBatchSize);
        ArgumentException.ThrowIfNullOrWhiteSpace(artworkDirectory);
        if (!Directory.Exists(artworkDirectory))
        {
            return new ArtworkCachePruneResult(0, 0);
        }

        var referencedPaths = await LoadReferencedArtworkPathsAsync(cancellationToken);
        var artworkRoot = Path.GetFullPath(artworkDirectory);
        var cutoff = cutoffUtc.UtcDateTime;
        var temporaryCutoff = temporaryCutoffUtc.UtcDateTime;
        // Candidates are materialized before any deletion so enumeration never observes
        // a directory this method is already mutating.
        var expired = new List<string>();
        foreach (var file in Directory.EnumerateFiles(artworkRoot, "*", SearchOption.TopDirectoryOnly))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var extension = Path.GetExtension(file);
            var isTemporary = string.Equals(extension, ".tmp", StringComparison.OrdinalIgnoreCase);
            if (!isTemporary && !RemovableExtensions.Contains(extension))
            {
                continue;
            }

            DateTime lastWriteUtc;
            try
            {
                lastWriteUtc = File.GetLastWriteTimeUtc(file);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                continue;
            }

            if (isTemporary ? lastWriteUtc >= temporaryCutoff : lastWriteUtc >= cutoff)
            {
                continue;
            }

            if (!isTemporary && referencedPaths.Contains(file))
            {
                continue;
            }

            expired.Add(file);
        }

        long deleted = 0;
        long failed = 0;
        for (var offset = 0; offset < expired.Count; offset += batchSize)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var end = Math.Min(offset + batchSize, expired.Count);
            for (var index = offset; index < end; index++)
            {
                try
                {
                    File.Delete(expired[index]);
                    deleted++;
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    // Locked or inaccessible files are skipped and retried on the next cycle.
                    failed++;
                }
            }
        }

        return new ArtworkCachePruneResult(deleted, failed);
    }

    private async Task<HashSet<string>> LoadReferencedArtworkPathsAsync(CancellationToken cancellationToken)
    {
        var referenced = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT artwork_path FROM plays WHERE artwork_path IS NOT NULL;";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var storedPath = reader.GetString(0);
            if (string.IsNullOrWhiteSpace(storedPath))
            {
                continue;
            }

            try
            {
                referenced.Add(Path.GetFullPath(storedPath));
            }
            catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
            {
                // A malformed stored path must not block cache cleanup.
            }
        }

        return referenced;
    }
}
