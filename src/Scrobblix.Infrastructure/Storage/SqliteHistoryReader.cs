using System.Globalization;
using Microsoft.Data.Sqlite;
using Scrobblix.Application.ReadModels;
using Scrobblix.Domain.Queue;

namespace Scrobblix.Infrastructure.Storage;

public sealed class SqliteHistoryReader(string connectionString)
{
    public async Task<TodaySummary> GetTodaySummaryAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var localDate = TimeZoneInfo.ConvertTime(now, TimeZoneInfo.Local).Date;
        var localStart = new DateTimeOffset(localDate, TimeZoneInfo.Local.GetUtcOffset(localDate));
        var localEnd = localStart.AddDays(1);
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                COUNT(*),
                COALESCE(SUM(CASE WHEN p.eligible_utc IS NOT NULL THEN 1 ELSE 0 END), 0),
                COALESCE(SUM(CASE WHEN EXISTS (
                    SELECT 1 FROM provider_actions a
                    WHERE a.play_id = p.play_id AND a.state = $accepted
                ) THEN 1 ELSE 0 END), 0),
                COALESCE(SUM(p.confirmed_played_ms), 0)
            FROM plays p
            WHERE p.started_utc >= $startUtc AND p.started_utc < $endUtc;
            """;
        command.Parameters.AddWithValue("$accepted", (int)ProviderActionState.Accepted);
        command.Parameters.AddWithValue("$startUtc", localStart.UtcDateTime.ToString("O", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$endUtc", localEnd.UtcDateTime.ToString("O", CultureInfo.InvariantCulture));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);
        return new TodaySummary(
            reader.GetInt32(0),
            reader.GetInt32(1),
            reader.GetInt32(2),
            reader.GetInt64(3));
    }

    public async Task<IReadOnlyList<RecentPlay>> GetRecentAsync(int limit, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1);
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT play_id, started_utc, artist, title, album, duration_ms, confirmed_played_ms, eligible_utc, terminal_reason_code, artwork_path
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
                reader.IsDBNull(8) ? null : reader.GetString(8),
                reader.IsDBNull(9) ? null : reader.GetString(9)));
        }

        return results;
    }
}
