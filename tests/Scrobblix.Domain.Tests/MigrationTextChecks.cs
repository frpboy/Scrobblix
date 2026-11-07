using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;
using Scrobblix.Infrastructure.Storage;

internal static class MigrationTextChecks
{
    public static async Task RunAsync()
    {
        foreach (var lineEnding in new[] { "\n", "\r\n" })
        {
            var path = Path.Combine(Path.GetTempPath(), "scrobblix-migration-text-" + Guid.NewGuid().ToString("N") + ".db");
            var cs = $"Data Source={path};Pooling=False";
            try
            {
                await new SqliteDatabase(cs).InitializeAsync(CancellationToken.None);
                await using var connection = new SqliteConnection(cs);
                await connection.OpenAsync();
                using (var sentinel = connection.CreateCommand())
                {
                    sentinel.CommandText = "CREATE TABLE synthetic_preserved(id INTEGER PRIMARY KEY,value TEXT); INSERT INTO synthetic_preserved VALUES(1,'must-survive-upgrade');";
                    await sentinel.ExecuteNonQueryAsync();
                }
                var assembly = typeof(SqliteDatabase).Assembly;
                var saved = new Dictionary<string,string>();
                foreach (var resource in assembly.GetManifestResourceNames().Where(x => x.Contains(".Storage.Migrations.",StringComparison.Ordinal)))
                {
                    using var stream = assembly.GetManifestResourceStream(resource)!;
                    using var reader = new StreamReader(stream);
                    var text = (await reader.ReadToEndAsync()).Replace("\r\n", "\n",StringComparison.Ordinal).Replace("\n",lineEnding,StringComparison.Ordinal);
                    var id = resource.Split(".Storage.Migrations.",StringSplitOptions.None)[1][..^4];
                    var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
                    using var update = connection.CreateCommand();
                    update.CommandText = "UPDATE schema_migrations SET checksum=$hash WHERE migration_id=$id";
                    update.Parameters.AddWithValue("$hash",hash); update.Parameters.AddWithValue("$id",id);
                    if (await update.ExecuteNonQueryAsync() != 1) throw new InvalidOperationException("Synthetic migration identity mismatch");
                    saved[id] = hash;
                }
                await new SqliteDatabase(cs).InitializeAsync(CancellationToken.None);
                using (var verify = connection.CreateCommand())
                {
                    verify.CommandText = "SELECT value FROM synthetic_preserved WHERE id=1";
                    if ((string?)await verify.ExecuteScalarAsync() != "must-survive-upgrade") throw new InvalidOperationException("Existing data changed");
                    verify.CommandText = "SELECT migration_id,checksum FROM schema_migrations";
                    using var rows = await verify.ExecuteReaderAsync();
                    while (await rows.ReadAsync()) if (saved[rows.GetString(0)] != rows.GetString(1)) throw new InvalidOperationException("Existing migration checksum rewritten");
                }
                using (var corrupt = connection.CreateCommand())
                {
                    corrupt.CommandText = "UPDATE schema_migrations SET checksum=$bad WHERE migration_id='0001_initial'";
                    corrupt.Parameters.AddWithValue("$bad",Convert.ToHexStringLower(SHA256.HashData("changed SQL"u8.ToArray())));
                    await corrupt.ExecuteNonQueryAsync();
                }
                var rejected = false;
                try { await new SqliteDatabase(cs).InitializeAsync(CancellationToken.None); }
                catch (InvalidOperationException ex) when (ex.Message.Contains("checksum mismatch",StringComparison.Ordinal)) { rejected = true; }
                if (!rejected) throw new InvalidOperationException("Changed migration checksum was accepted");
                Console.WriteLine($"PASS known {(lineEnding == "\n" ? "LF" : "CRLF")} migration checksums reopen without rewriting history/data; changed SQL rejected");
            }
            finally { File.Delete(path); }
        }
    }
}
