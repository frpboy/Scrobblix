using System.Globalization;
using Microsoft.Data.Sqlite;
using Scrobblix.Application.ReadModels;

namespace Scrobblix.Infrastructure.Storage;

public sealed class SqliteHistoryReader(string connectionString)
{
    public async Task<IReadOnlyList<RecentPlay>> GetRecentAsync(int limit, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1);
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT play_id, started_utc, artist, title, album, duration_ms, confirmed_played_ms, eligible_utc, terminal_reason_code
            FROM plays
            ORDER BY started_utc DESC, created_utc DESC, play_id DESC
            LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$limit", limit);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var results = new List<RecentPlay>();
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new RecentPlay(
                reader.GetString(0),
                DateTimeOffset.Parse(reader.GetString(1), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetInt32(5),
                reader.GetInt64(6),
                reader.IsDBNull(7) ? null : DateTimeOffset.Parse(reader.GetString(7), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
                reader.IsDBNull(8) ? null : reader.GetString(8)));
        }

        return results;
    }
}
