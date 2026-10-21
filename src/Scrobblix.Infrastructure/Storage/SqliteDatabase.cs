using System.Reflection;
using Microsoft.Data.Sqlite;

namespace Scrobblix.Infrastructure.Storage;

public sealed class SqliteDatabase(string connectionString)
{
    private const string MigrationResourceName = "Scrobblix.Infrastructure.Storage.Migrations.0001_initial.sql";

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        var assembly = typeof(SqliteDatabase).Assembly;
        await using var stream = assembly.GetManifestResourceStream(MigrationResourceName)
            ?? throw new InvalidOperationException($"Missing embedded migration '{MigrationResourceName}'.");
        using var reader = new StreamReader(stream);
        var sql = await reader.ReadToEndAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
