using System.Globalization;
using Microsoft.Data.Sqlite;
using Scrobblix.Application.ReadModels;
using Scrobblix.Domain.Queue;

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
            SELECT play_id, started_utc, artist, title, album, duration_ms, confirmed_played_ms,
                   eligible_utc, terminal_reason_code, artwork_path, album_artist, track_number,
                   release_year, recording_mbid, release_mbid, artwork_source
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
                reader.IsDBNull(9) ? null : reader.GetString(9),
                reader.IsDBNull(10) ? null : reader.GetString(10),
                reader.IsDBNull(11) ? null : reader.GetInt32(11),
                reader.IsDBNull(12) ? null : reader.GetInt32(12),
                reader.IsDBNull(13) ? null : reader.GetString(13),
                reader.IsDBNull(14) ? null : reader.GetString(14),
                reader.IsDBNull(15) ? null : reader.GetString(15)));
        }

        return results;
    }

    public async Task<TodaySummary> GetTodaySummaryAsync(DateTimeOffset localNow, CancellationToken cancellationToken)
    {
        var localDateTime = TimeZoneInfo.ConvertTime(localNow, TimeZoneInfo.Local).DateTime;
        var localStartDate = DateTime.SpecifyKind(localDateTime.Date, DateTimeKind.Unspecified);
        var localEndDate = localStartDate.AddDays(1);
        var startUtc = new DateTimeOffset(localStartDate, TimeZoneInfo.Local.GetUtcOffset(localStartDate))
            .UtcDateTime.ToString("O", CultureInfo.InvariantCulture);
        var endUtc = new DateTimeOffset(localEndDate, TimeZoneInfo.Local.GetUtcOffset(localEndDate))
            .UtcDateTime.ToString("O", CultureInfo.InvariantCulture);

        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        int sessionCount;
        int eligibleCount;
        long confirmedMilliseconds;
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                SELECT COUNT(*),
                       COALESCE(SUM(CASE WHEN eligible_utc IS NOT NULL THEN 1 ELSE 0 END), 0),
                       COALESCE(SUM(confirmed_played_ms), 0)
                FROM plays
                WHERE started_utc >= $startUtc AND started_utc < $endUtc;
                """;
            command.Parameters.AddWithValue("$startUtc", startUtc);
            command.Parameters.AddWithValue("$endUtc", endUtc);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            await reader.ReadAsync(cancellationToken);
            sessionCount = reader.GetInt32(0);
            eligibleCount = reader.GetInt32(1);
            confirmedMilliseconds = reader.GetInt64(2);
        }

        int acceptedCount;
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                SELECT COUNT(*)
                FROM provider_actions a
                INNER JOIN plays p ON p.play_id = a.play_id
                WHERE a.provider = 'lastfm'
                  AND a.action_type = $scrobble
                  AND a.state = $accepted
                  AND p.started_utc >= $startUtc
                  AND p.started_utc < $endUtc;
                """;
            command.Parameters.AddWithValue("$scrobble", (int)ProviderActionType.Scrobble);
            command.Parameters.AddWithValue("$accepted", (int)ProviderActionState.Accepted);
            command.Parameters.AddWithValue("$startUtc", startUtc);
            command.Parameters.AddWithValue("$endUtc", endUtc);
            acceptedCount = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture);
        }

        return new TodaySummary(sessionCount, eligibleCount, acceptedCount, confirmedMilliseconds);
    }

    public async Task<ListeningStatistics> GetStatisticsAsync(CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        int totalSessions;
        int eligiblePlays;
        int uniqueArtists;
        long confirmedMilliseconds;
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                SELECT COUNT(*),
                       COALESCE(SUM(CASE WHEN eligible_utc IS NOT NULL THEN 1 ELSE 0 END), 0),
                       COUNT(DISTINCT NULLIF(TRIM(artist), '')),
                       COALESCE(SUM(confirmed_played_ms), 0)
                FROM plays;
                """;
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            await reader.ReadAsync(cancellationToken);
            totalSessions = reader.GetInt32(0);
            eligiblePlays = reader.GetInt32(1);
            uniqueArtists = reader.GetInt32(2);
            confirmedMilliseconds = reader.GetInt64(3);
        }

        var thirtyDaysAgoUtc = DateTimeOffset.UtcNow.AddDays(-30).UtcDateTime
            .ToString("O", CultureInfo.InvariantCulture);
        int sessionsLast30Days;
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT COUNT(*) FROM plays WHERE started_utc >= $sinceUtc;";
            command.Parameters.AddWithValue("$sinceUtc", thirtyDaysAgoUtc);
            sessionsLast30Days = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture);
        }

        var topArtists = new List<TopArtistStatistic>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                SELECT artist, COUNT(*), COALESCE(SUM(confirmed_played_ms), 0)
                FROM plays
                WHERE artist IS NOT NULL AND TRIM(artist) <> ''
                GROUP BY artist COLLATE NOCASE
                ORDER BY COUNT(*) DESC, SUM(confirmed_played_ms) DESC, artist COLLATE NOCASE ASC
                LIMIT 10;
                """;
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                topArtists.Add(new TopArtistStatistic(
                    reader.GetString(0),
                    reader.GetInt32(1),
                    reader.GetInt64(2)));
            }
        }

        return new ListeningStatistics(
            totalSessions,
            eligiblePlays,
            uniqueArtists,
            confirmedMilliseconds,
            sessionsLast30Days,
            topArtists);
    }
}
