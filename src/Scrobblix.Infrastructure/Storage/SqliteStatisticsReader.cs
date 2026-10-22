using System.Globalization;
using Microsoft.Data.Sqlite;
using Scrobblix.Application.ReadModels;
using Scrobblix.Domain.Queue;

namespace Scrobblix.Infrastructure.Storage;

public sealed class SqliteStatisticsReader(string connectionString)
{
    public async Task<ListeningStatistics> GetAsync(CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        int totalSessions;
        int eligibleSessions;
        long confirmedMilliseconds;
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                SELECT COUNT(*),
                       COALESCE(SUM(CASE WHEN eligible_utc IS NOT NULL THEN 1 ELSE 0 END), 0),
                       COALESCE(SUM(confirmed_played_ms), 0)
                FROM plays;
                """;
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            await reader.ReadAsync(cancellationToken);
            totalSessions = reader.GetInt32(0);
            eligibleSessions = reader.GetInt32(1);
            confirmedMilliseconds = reader.GetInt64(2);
        }
        int acceptedScrobbles;
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                SELECT COUNT(DISTINCT play_id) FROM provider_actions
                WHERE provider = $provider AND action_type = $actionType AND state = $accepted;
                """;
            command.Parameters.AddWithValue("$provider", "lastfm");
            command.Parameters.AddWithValue("$actionType", (int)ProviderActionType.Scrobble);
            command.Parameters.AddWithValue("$accepted", (int)ProviderActionState.Accepted);
            acceptedScrobbles = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture);
        }
        var topArtists = new List<ArtistListeningSummary>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                SELECT TRIM(artist), COUNT(*), COALESCE(SUM(confirmed_played_ms), 0)
                FROM plays WHERE artist IS NOT NULL AND TRIM(artist) <> ''
                GROUP BY TRIM(artist)
                ORDER BY 3 DESC, 2 DESC, TRIM(artist) COLLATE NOCASE ASC
                LIMIT 10;
                """;
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
                topArtists.Add(new ArtistListeningSummary(reader.GetString(0), reader.GetInt32(1), reader.GetInt64(2)));
        }
        return new ListeningStatistics(totalSessions, eligibleSessions, acceptedScrobbles, confirmedMilliseconds, topArtists);
    }
}
