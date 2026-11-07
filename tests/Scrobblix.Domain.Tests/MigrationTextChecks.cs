using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
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
                    sentinel.CommandText = """
                        INSERT INTO plays (play_id,started_utc,artist,title,confirmed_played_ms,local_status,created_utc,updated_utc)
                        VALUES ('synthetic-play','2026-01-01T00:00:00Z','Synthetic Artist','Synthetic Title',42000,0,'2026-01-01T00:00:00Z','2026-01-01T00:00:00Z');
                        INSERT INTO accounts (account_id,provider,remote_username,credential_resource,is_connected,created_utc,updated_utc)
                        VALUES ('synthetic-account','lastfm','synthetic-user','synthetic-resource',1,'2026-01-01T00:00:00Z','2026-01-01T00:00:00Z');
                        INSERT INTO provider_actions (action_id,play_id,account_id,provider,action_type,state,idempotency_key,attempt_count,due_utc,created_utc,updated_utc)
                        VALUES ('synthetic-action','synthetic-play','synthetic-account','lastfm',0,0,'synthetic-key',2,'2026-01-01T00:00:00Z','2026-01-01T00:00:00Z','2026-01-01T00:00:00Z');
                        INSERT INTO loved_tracks (track_key,source_play_id,artist,title,is_loved,current_action_id,created_utc,updated_utc)
                        VALUES ('synthetic-track','synthetic-play','Synthetic Artist','Synthetic Title',1,'synthetic-action','2026-01-01T00:00:00Z','2026-01-01T00:00:00Z');
                        """;
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
                var before = await SnapshotAsync(connection);
                if (saved.Count != 6) throw new InvalidOperationException("Not all migration resources were exercised");
                await new SqliteDatabase(cs).InitializeAsync(CancellationToken.None);
                if (await SnapshotAsync(connection) != before)
                    throw new InvalidOperationException("Existing history/accounts/queue/loved tracks or migration records changed");
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
    private static async Task<string> SnapshotAsync(SqliteConnection connection)
    {
        var tables = new[] { "schema_migrations", "plays", "accounts", "provider_actions", "loved_tracks" };
        var result = new List<List<object?[]>>();
        foreach (var table in tables)
        {
            using var query = connection.CreateCommand();
            // Table names are fixed test constants, never external input.
            query.CommandText = $"SELECT * FROM {table} ORDER BY 1";
            using var rows = await query.ExecuteReaderAsync();
            var values = new List<object?[]>();
            while (await rows.ReadAsync())
            {
                var row = new object?[rows.FieldCount];
                for (var i = 0; i < row.Length; i++) row[i] = rows.IsDBNull(i) ? null : rows.GetValue(i);
                values.Add(row);
            }
            result.Add(values);
        }
        return JsonSerializer.Serialize(result);
    }

}
