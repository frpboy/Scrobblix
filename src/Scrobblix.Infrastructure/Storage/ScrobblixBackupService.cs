using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace Scrobblix.Infrastructure.Storage;

/// <summary>
/// Versioned, local-only Scrobblix database backups. Credentials, session keys and artwork
/// are deliberately excluded. Restore is staged and only applied on the next app launch,
/// after the old process has stopped all database writers.
/// </summary>
public sealed class ScrobblixBackupService
{
    public const int CurrentFormatVersion = 1;
    public const int CurrentSchemaVersion = 4;
    private const long MaximumDatabaseBytes = 2L * 1024 * 1024 * 1024;
    private const long MaximumArchiveBytes = 2L * 1024 * 1024 * 1024;
    private const string DatabaseEntryName = "scrobblix.db";
    private const string ManifestEntryName = "manifest.json";
    private const string PendingDirectoryName = "restore-pending";
    private const string PendingDatabaseName = "database.staged";
    private const string PendingManifestName = "pending-manifest.json";
    private static readonly string[] ExpectedMigrations =
    [
        "0001_initial",
        "0002_accounts",
        "0003_local_history_artwork",
        "0004_richer_metadata",
    ];
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public static async Task CreateBackupAsync(
        string databaseConnectionString,
        string destinationArchivePath,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databaseConnectionString);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationArchivePath);
        cancellationToken.ThrowIfCancellationRequested();

        var destination = Path.GetFullPath(destinationArchivePath);
        var connectionBuilder = new SqliteConnectionStringBuilder(databaseConnectionString);
        if (!string.IsNullOrWhiteSpace(connectionBuilder.DataSource)
            && string.Equals(Path.GetFullPath(connectionBuilder.DataSource), destination, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The backup destination cannot overwrite the active database.");
        }

        var destinationDirectory = Path.GetDirectoryName(destination)
            ?? throw new InvalidOperationException("The backup destination has no parent directory.");
        Directory.CreateDirectory(destinationDirectory);

        var snapshotPath = Path.Combine(Path.GetTempPath(), $"scrobblix-snapshot-{Guid.NewGuid():N}.db");
        var temporaryArchivePath = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await CreateDatabaseSnapshotAsync(databaseConnectionString, snapshotPath, cancellationToken);
            var databaseInfo = new FileInfo(snapshotPath);
            if (!databaseInfo.Exists || databaseInfo.Length is <= 0 or > MaximumDatabaseBytes)
            {
                throw new InvalidDataException("The SQLite snapshot size is invalid for a Scrobblix backup.");
            }

            var manifest = new BackupManifest(
                CurrentFormatVersion,
                CurrentSchemaVersion,
                DateTimeOffset.UtcNow,
                DatabaseEntryName,
                databaseInfo.Length,
                await ComputeSha256Async(snapshotPath, cancellationToken),
                typeof(ScrobblixBackupService).Assembly.GetName().Version?.ToString() ?? "unknown");

            await using (var archiveStream = new FileStream(
                temporaryArchivePath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None,
                131072, FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                using (var archive = new ZipArchive(archiveStream, ZipArchiveMode.Create, leaveOpen: true))
                {
                    var databaseEntry = archive.CreateEntry(DatabaseEntryName, CompressionLevel.Optimal);
                    await using (var output = databaseEntry.Open())
                    await using (var input = new FileStream(
                        snapshotPath, FileMode.Open, FileAccess.Read, FileShare.Read,
                        131072, FileOptions.Asynchronous | FileOptions.SequentialScan))
                    {
                        await input.CopyToAsync(output, 131072, cancellationToken);
                    }

                    var manifestEntry = archive.CreateEntry(ManifestEntryName, CompressionLevel.Optimal);
                    await using (var manifestStream = manifestEntry.Open())
                    {
                        await JsonSerializer.SerializeAsync(manifestStream, manifest, JsonOptions, cancellationToken);
                    }
                }

                await archiveStream.FlushAsync(cancellationToken);
                archiveStream.Flush(flushToDisk: true);
            }

            var archiveInfo = new FileInfo(temporaryArchivePath);
            if (!archiveInfo.Exists || archiveInfo.Length <= 0 || archiveInfo.Length > MaximumArchiveBytes)
            {
                throw new InvalidDataException("The completed backup archive has an invalid size.");
            }

            File.Move(temporaryArchivePath, destination, overwrite: true);
        }
        finally
        {
            TryDelete(snapshotPath);
            TryDelete(temporaryArchivePath);
        }
    }

    /// <summary>
    /// Validates a selected backup completely and stages it. The live database is not
    /// modified by this method. The caller should close the app after a successful stage.
    /// </summary>
    public static async Task StageRestoreAsync(
        string archivePath,
        string dataDirectory,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(archivePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);
        var archiveFile = new FileInfo(Path.GetFullPath(archivePath));
        if (!archiveFile.Exists || archiveFile.Length is <= 0 or > MaximumArchiveBytes)
        {
            throw new InvalidDataException("The selected file is missing or too large to be a Scrobblix backup.");
        }

        var stageDirectory = Path.Combine(Path.GetFullPath(dataDirectory), PendingDirectoryName);
        Directory.CreateDirectory(stageDirectory);
        var stagedDatabase = Path.Combine(stageDirectory, PendingDatabaseName);
        var stagedManifest = Path.Combine(stageDirectory, PendingManifestName);
        var tempDatabase = stagedDatabase + "." + Guid.NewGuid().ToString("N") + ".tmp";
        var tempManifest = stagedManifest + "." + Guid.NewGuid().ToString("N") + ".tmp";

        try
        {
            BackupManifest manifest;
            await using (var archiveStream = new FileStream(
                archiveFile.FullName, FileMode.Open, FileAccess.Read, FileShare.Read,
                131072, FileOptions.Asynchronous | FileOptions.SequentialScan))
            using (var archive = new ZipArchive(archiveStream, ZipArchiveMode.Read, leaveOpen: false))
            {
                if (archive.Entries.Count != 2)
                {
                    throw new InvalidDataException("The backup must contain only a manifest and one SQLite snapshot.");
                }

                var manifestEntries = archive.Entries.Where(entry => entry.FullName == ManifestEntryName).ToArray();
                var databaseEntries = archive.Entries.Where(entry => entry.FullName == DatabaseEntryName).ToArray();
                if (manifestEntries.Length != 1 || databaseEntries.Length != 1)
                {
                    throw new InvalidDataException("The backup is missing its manifest or database snapshot.");
                }

                if (databaseEntries[0].Length is <= 0 or > MaximumDatabaseBytes)
                {
                    throw new InvalidDataException("The database snapshot length is outside the supported range.");
                }

                await using (var manifestStream = manifestEntries[0].Open())
                {
                    manifest = await JsonSerializer.DeserializeAsync<BackupManifest>(
                        manifestStream, JsonOptions, cancellationToken)
                        ?? throw new InvalidDataException("The backup manifest is empty.");
                }

                ValidateManifest(manifest);
                if (databaseEntries[0].Length != manifest.DatabaseBytes)
                {
                    throw new InvalidDataException("The database entry length does not match the manifest.");
                }

                await using var source = databaseEntries[0].Open();
                await using var destination = new FileStream(
                    tempDatabase, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                    131072, FileOptions.Asynchronous | FileOptions.SequentialScan);
                await CopyBoundedAsync(source, destination, MaximumDatabaseBytes, cancellationToken);
                await destination.FlushAsync(cancellationToken);
                destination.Flush(flushToDisk: true);
            }

            var stagedInfo = new FileInfo(tempDatabase);
            if (stagedInfo.Length != manifest.DatabaseBytes)
            {
                throw new InvalidDataException("The extracted database length does not match the manifest.");
            }

            var actualHash = await ComputeSha256Async(tempDatabase, cancellationToken);
            if (!string.Equals(actualHash, manifest.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("The database checksum does not match the backup manifest.");
            }

            await ValidateDatabaseAsync(tempDatabase, cancellationToken);
            await using (var manifestOutput = new FileStream(
                tempManifest, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                16384, FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                await JsonSerializer.SerializeAsync(manifestOutput, manifest, JsonOptions, cancellationToken);
                await manifestOutput.FlushAsync(cancellationToken);
                manifestOutput.Flush(flushToDisk: true);
            }

            File.Move(tempDatabase, stagedDatabase, overwrite: true);
            File.Move(tempManifest, stagedManifest, overwrite: true);
        }
        finally
        {
            TryDelete(tempDatabase);
            TryDelete(tempManifest);
        }
    }

    /// <summary>
    /// Applies a previously staged restore before application database services are created.
    /// Returns null when there is no staged restore, otherwise returns the safety snapshot
    /// path (or an empty string if the installation did not yet have a database).
    /// </summary>
    public static async Task<string?> ApplyStagedRestoreAsync(
        string dataDirectory,
        string activeDatabasePath,
        CancellationToken cancellationToken)
    {
        var fullDataDirectory = Path.GetFullPath(dataDirectory);
        var stageDirectory = Path.Combine(fullDataDirectory, PendingDirectoryName);
        var stagedDatabase = Path.Combine(stageDirectory, PendingDatabaseName);
        var stagedManifest = Path.Combine(stageDirectory, PendingManifestName);
        if (!File.Exists(stagedDatabase) && !File.Exists(stagedManifest))
        {
            return null;
        }

        if (!File.Exists(stagedDatabase) || !File.Exists(stagedManifest))
        {
            throw new InvalidDataException($"A staged Scrobblix restore is incomplete. Inspect '{stageDirectory}' and restore from a valid backup.");
        }

        var manifest = await ReadManifestAsync(stagedManifest, cancellationToken);
        ValidateManifest(manifest);
        var stagedInfo = new FileInfo(stagedDatabase);
        if (!stagedInfo.Exists || stagedInfo.Length != manifest.DatabaseBytes
            || !string.Equals(await ComputeSha256Async(stagedDatabase, cancellationToken), manifest.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("The staged database failed its size or checksum validation.");
        }

        await ValidateDatabaseAsync(stagedDatabase, cancellationToken);

        var activeDatabase = Path.GetFullPath(activeDatabasePath);
        Directory.CreateDirectory(Path.GetDirectoryName(activeDatabase)!);
        var safetySnapshotPath = string.Empty;
        var tempReplacement = activeDatabase + "." + Guid.NewGuid().ToString("N") + ".restore";
        try
        {
            if (File.Exists(activeDatabase))
            {
                var backupDirectory = Path.Combine(fullDataDirectory, "backups");
                Directory.CreateDirectory(backupDirectory);
                safetySnapshotPath = Path.Combine(
                    backupDirectory,
                    $"Scrobblix-before-restore-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid().ToString("N")[..8]}.db");
                await CreateDatabaseSnapshotAsync(
                    new SqliteConnectionStringBuilder { DataSource = activeDatabase, Pooling = false }.ToString(),
                    safetySnapshotPath,
                    cancellationToken);
            }

            TryDelete(activeDatabase + "-wal");
            TryDelete(activeDatabase + "-shm");
            File.Copy(stagedDatabase, tempReplacement, overwrite: false);
            File.Move(tempReplacement, activeDatabase, overwrite: true);

            TryDelete(stagedDatabase);
            TryDelete(stagedManifest);
            try { Directory.Delete(stageDirectory, recursive: false); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            return safetySnapshotPath;
        }
        catch
        {
            TryDelete(tempReplacement);
            throw;
        }
    }

    private static void ValidateManifest(BackupManifest manifest)
    {
        if (manifest.FormatVersion != CurrentFormatVersion)
        {
            throw new InvalidDataException($"Unsupported Scrobblix backup format version: {manifest.FormatVersion}.");
        }

        if (manifest.SchemaVersion != CurrentSchemaVersion)
        {
            throw new InvalidDataException(
                $"This backup uses schema {manifest.SchemaVersion}; this build supports schema {CurrentSchemaVersion}.");
        }

        if (!string.Equals(manifest.DatabaseEntry, DatabaseEntryName, StringComparison.Ordinal)
            || manifest.DatabaseBytes is <= 0 or > MaximumDatabaseBytes
            || string.IsNullOrWhiteSpace(manifest.Sha256)
            || manifest.Sha256.Length != 64
            || manifest.Sha256.Any(character => !Uri.IsHexDigit(character)))
        {
            throw new InvalidDataException("The backup manifest contains invalid database metadata.");
        }
    }

    private static async Task<BackupManifest> ReadManifestAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
            16384, FileOptions.Asynchronous | FileOptions.SequentialScan);
        return await JsonSerializer.DeserializeAsync<BackupManifest>(stream, JsonOptions, cancellationToken)
            ?? throw new InvalidDataException("The staged backup manifest is empty.");
    }

    private static async Task CreateDatabaseSnapshotAsync(
        string sourceConnectionString,
        string destinationPath,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        TryDelete(destinationPath);
        var destinationConnectionString = new SqliteConnectionStringBuilder
        {
            DataSource = destinationPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false,
        }.ToString();

        await using var source = new SqliteConnection(sourceConnectionString);
        await source.OpenAsync(cancellationToken);
        await using var destination = new SqliteConnection(destinationConnectionString);
        await destination.OpenAsync(cancellationToken);
        source.BackupDatabase(destination);
    }

    private static async Task ValidateDatabaseAsync(string databasePath, CancellationToken cancellationToken)
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false,
        }.ToString();
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await using (var integrity = connection.CreateCommand())
        {
            integrity.CommandText = "PRAGMA integrity_check;";
            var result = await integrity.ExecuteScalarAsync(cancellationToken) as string;
            if (!string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("The SQLite integrity check failed for this backup.");
            }
        }

        var migrations = new List<string>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT migration_id FROM schema_migrations ORDER BY migration_id;";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                migrations.Add(reader.GetString(0));
            }
        }

        if (!migrations.SequenceEqual(ExpectedMigrations, StringComparer.Ordinal))
        {
            throw new InvalidDataException("The backup schema migrations do not match this Scrobblix build.");
        }
    }

    private static async Task<string> ComputeSha256Async(string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
            131072, FileOptions.Asynchronous | FileOptions.SequentialScan);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexStringLower(hash);
    }

    private static async Task CopyBoundedAsync(
        Stream source,
        Stream destination,
        long maximumBytes,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[131072];
        long total = 0;
        while (true)
        {
            var read = await source.ReadAsync(buffer.AsMemory(), cancellationToken);
            if (read == 0) break;
            total += read;
            if (total > maximumBytes)
            {
                throw new InvalidDataException("The uncompressed database exceeds the supported backup size.");
            }

            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private sealed record BackupManifest(
        int FormatVersion,
        int SchemaVersion,
        DateTimeOffset CreatedUtc,
        string DatabaseEntry,
        long DatabaseBytes,
        string Sha256,
        string AppVersion);
}
