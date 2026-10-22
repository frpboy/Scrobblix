using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;

namespace Scrobblix.Infrastructure.Storage;

public sealed class SqliteDatabase(string connectionString)
{
    private static readonly (string Id, string ResourceName)[] Migrations =
    [
        ("0001_initial", "Scrobblix.Infrastructure.Storage.Migrations.0001_initial.sql"),
        ("0002_accounts", "Scrobblix.Infrastructure.Storage.Migrations.0002_accounts.sql"),
        ("0003_local_history_artwork", "Scrobblix.Infrastructure.Storage.Migrations.0003_local_history_artwork.sql"),
        ("0004_richer_metadata", "Scrobblix.Infrastructure.Storage.Migrations.0004_richer_metadata.sql"),
        ("0005_loved_tracks", "Scrobblix.Infrastructure.Storage.Migrations.0005_loved_tracks.sql"),
    ];

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await EnsureMigrationTableAsync(connection, cancellationToken);
        await ValidateAppliedMigrationsAsync(connection, cancellationToken);
        var assembly = typeof(SqliteDatabase).Assembly;
        foreach (var migration in Migrations)
        {
            await ApplyMigrationAsync(connection, assembly, migration, cancellationToken);
        }
    }

    private static async Task ValidateAppliedMigrationsAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        var appliedMigrations = new List<string>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT migration_id FROM schema_migrations ORDER BY migration_id;";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                appliedMigrations.Add(reader.GetString(0));
            }
        }

        var knownMigrationIds = Migrations.Select(migration => migration.Id).ToArray();
        var unknownMigration = appliedMigrations.FirstOrDefault(
            migrationId => !knownMigrationIds.Contains(migrationId, StringComparer.Ordinal));
        if (unknownMigration is not null)
        {
            throw new InvalidOperationException(
                $"Database migration '{unknownMigration}' is not recognized by this Scrobblix build. The database may be newer than this application; do not continue with a downgrade.");
        }

        if (appliedMigrations.Count > knownMigrationIds.Length
            || !appliedMigrations.SequenceEqual(
                knownMigrationIds.Take(appliedMigrations.Count),
                StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                "The database migration history is not a valid prefix of this Scrobblix build. Restore a valid backup or use the matching application version.");
        }
    }

    private static async Task EnsureMigrationTableAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS schema_migrations (
                migration_id TEXT PRIMARY KEY,
                checksum TEXT NOT NULL,
                applied_utc TEXT NOT NULL
            );
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task ApplyMigrationAsync(
        SqliteConnection connection,
        Assembly assembly,
        (string Id, string ResourceName) migration,
        CancellationToken cancellationToken)
    {
        await using var stream = assembly.GetManifestResourceStream(migration.ResourceName)
            ?? throw new InvalidOperationException($"Missing embedded migration '{migration.ResourceName}'.");
        using var reader = new StreamReader(stream);
        var sql = await reader.ReadToEndAsync(cancellationToken);
        var checksum = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(sql)));

        await using var lookup = connection.CreateCommand();
        lookup.CommandText = "SELECT checksum FROM schema_migrations WHERE migration_id = $id;";
        lookup.Parameters.AddWithValue("$id", migration.Id);
        var appliedChecksum = await lookup.ExecuteScalarAsync(cancellationToken) as string;
        if (appliedChecksum is not null)
        {
            if (!string.Equals(appliedChecksum, checksum, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Migration checksum mismatch for '{migration.Id}'.");
            }

            return;
        }

        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = sql;
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var record = connection.CreateCommand())
        {
            record.Transaction = transaction;
            record.CommandText = "INSERT INTO schema_migrations (migration_id, checksum, applied_utc) VALUES ($id, $checksum, $appliedUtc);";
            record.Parameters.AddWithValue("$id", migration.Id);
            record.Parameters.AddWithValue("$checksum", checksum);
            record.Parameters.AddWithValue("$appliedUtc", DateTimeOffset.UtcNow.ToString("O", System.Globalization.CultureInfo.InvariantCulture));
            await record.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }
}
