using System.Globalization;
using Microsoft.Data.Sqlite;
using Scrobblix.Application.ReadModels;
using Scrobblix.Domain.Queue;

namespace Scrobblix.Infrastructure.Storage;

public sealed class SqliteQueueReader(string connectionString)
{
    public async Task<QueueSummary> GetSummaryAsync(CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                COALESCE(SUM(CASE WHEN state = $pending THEN 1 ELSE 0 END), 0),
                COALESCE(SUM(CASE WHEN state = $inFlight THEN 1 ELSE 0 END), 0),
                COALESCE(SUM(CASE WHEN state = $accepted THEN 1 ELSE 0 END), 0),
                COALESCE(SUM(CASE WHEN state = $retry THEN 1 ELSE 0 END), 0),
                COALESCE(SUM(CASE WHEN state = $authBlocked THEN 1 ELSE 0 END), 0),
                COALESCE(SUM(CASE WHEN state IN ($permanentFailure, $retryExhausted) THEN 1 ELSE 0 END), 0),
                COALESCE(SUM(CASE WHEN state = $outcomeUnknown THEN 1 ELSE 0 END), 0)
            FROM provider_actions;
            """;
        command.Parameters.AddWithValue("$pending", (int)ProviderActionState.Pending);
        command.Parameters.AddWithValue("$inFlight", (int)ProviderActionState.InFlight);
        command.Parameters.AddWithValue("$accepted", (int)ProviderActionState.Accepted);
        command.Parameters.AddWithValue("$retry", (int)ProviderActionState.RetryScheduled);
        command.Parameters.AddWithValue("$authBlocked", (int)ProviderActionState.AuthBlocked);
        command.Parameters.AddWithValue("$permanentFailure", (int)ProviderActionState.PermanentFailure);
        command.Parameters.AddWithValue("$retryExhausted", (int)ProviderActionState.RetryExhausted);
        command.Parameters.AddWithValue("$outcomeUnknown", (int)ProviderActionState.SubmissionOutcomeUnknown);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);
        return new QueueSummary(
            reader.GetInt32(0),
            reader.GetInt32(1),
            reader.GetInt32(2),
            reader.GetInt32(3),
            reader.GetInt32(4),
            reader.GetInt32(5),
            reader.GetInt32(6));
    }


    /// <summary>
    /// Returns only Last.fm scrobble actions that exhausted automatic retries and can be
    /// explicitly retried by the user. Scheduled retries and ambiguous outcomes are excluded.
    /// </summary>
    public async Task<IReadOnlyList<QueueActionSummary>> GetRetryableFailedScrobblesAsync(CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT a.action_id, a.play_id, a.provider, a.action_type, a.state, a.attempt_count,
                   a.due_utc, a.last_reason_code, p.artist, p.title
            FROM provider_actions a
            LEFT JOIN plays p ON p.play_id = a.play_id
            WHERE a.provider = $provider
              AND a.action_type = $scrobble
              AND a.state = $retryExhausted
            ORDER BY a.updated_utc DESC, a.created_utc DESC, a.action_id DESC;
            """;
        command.Parameters.AddWithValue("$provider", "lastfm");
        command.Parameters.AddWithValue("$scrobble", (int)ProviderActionType.Scrobble);
        command.Parameters.AddWithValue("$retryExhausted", (int)ProviderActionState.RetryExhausted);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var result = new List<QueueActionSummary>();
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new QueueActionSummary(
                reader.GetString(0),
                reader.IsDBNull(1) ? null : reader.GetString(1),
                reader.GetString(2),
                (ProviderActionType)reader.GetInt32(3),
                (ProviderActionState)reader.GetInt32(4),
                reader.GetInt32(5),
                DateTimeOffset.Parse(reader.GetString(6), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
                reader.IsDBNull(7) ? null : reader.GetString(7),
                reader.IsDBNull(8) ? null : reader.GetString(8),
                reader.IsDBNull(9) ? null : reader.GetString(9)));
        }

        return result;
    }

    public async Task<IReadOnlyList<LovedTrackSummary>> GetRecentLovedTracksAsync(int limit, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1);
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT p.play_id, p.artist, p.title, p.album, p.artwork_path,
                   a.action_type, a.state, a.attempt_count, a.last_reason_code, a.updated_utc
            FROM provider_actions a
            JOIN plays p ON p.play_id = a.play_id
            WHERE a.provider = 'lastfm'
              AND a.action_type IN ($love, $unlove)
              AND a.action_id = (
                  SELECT newer.action_id
                  FROM provider_actions newer
                  JOIN plays np ON np.play_id = newer.play_id
                  WHERE newer.provider = a.provider
                    AND newer.account_id = a.account_id
                    AND newer.action_type IN ($love, $unlove)
                    AND LOWER(TRIM(COALESCE(np.artist, ''))) = LOWER(TRIM(COALESCE(p.artist, '')))
                    AND LOWER(TRIM(COALESCE(np.title, ''))) = LOWER(TRIM(COALESCE(p.title, '')))
                  ORDER BY newer.created_utc DESC, newer.action_id DESC
                  LIMIT 1
              )
            ORDER BY a.updated_utc DESC, a.action_id DESC
            LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$love", (int)ProviderActionType.Love);
        command.Parameters.AddWithValue("$unlove", (int)ProviderActionType.Unlove);
        command.Parameters.AddWithValue("$limit", limit);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var result = new List<LovedTrackSummary>();
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new LovedTrackSummary(
                reader.GetString(0),
                reader.IsDBNull(1) ? null : reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetString(4),
                (ProviderActionType)reader.GetInt32(5),
                (ProviderActionState)reader.GetInt32(6),
                reader.GetInt32(7),
                reader.IsDBNull(8) ? null : reader.GetString(8),
                DateTimeOffset.Parse(reader.GetString(9), System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind)));
        }
        return result;
    }

    public async Task<IReadOnlyList<QueueActionSummary>> GetRecentActionsAsync(int limit, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1);
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT a.action_id, a.play_id, a.provider, a.action_type, a.state, a.attempt_count,
                   a.due_utc, a.last_reason_code, p.artist, p.title, a.account_id
            FROM provider_actions a
            LEFT JOIN plays p ON p.play_id = a.play_id
            ORDER BY a.updated_utc DESC, a.created_utc DESC, a.action_id DESC
            LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$limit", limit);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var result = new List<QueueActionSummary>();
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new QueueActionSummary(
                reader.GetString(0),
                reader.IsDBNull(1) ? null : reader.GetString(1),
                reader.GetString(2),
                (ProviderActionType)reader.GetInt32(3),
                (ProviderActionState)reader.GetInt32(4),
                reader.GetInt32(5),
                DateTimeOffset.Parse(reader.GetString(6), System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind),
                reader.IsDBNull(7) ? null : reader.GetString(7),
                reader.IsDBNull(8) ? null : reader.GetString(8),
                reader.IsDBNull(9) ? null : reader.GetString(9))
            {
                AccountId = reader.IsDBNull(10) ? null : reader.GetString(10),
            });
        }

        return result;
    }


    }

}
