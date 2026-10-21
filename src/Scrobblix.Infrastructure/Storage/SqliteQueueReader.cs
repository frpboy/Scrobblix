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
                COALESCE(SUM(CASE WHEN state = $permanentFailure THEN 1 ELSE 0 END), 0)
            FROM provider_actions;
            """;
        command.Parameters.AddWithValue("$pending", (int)ProviderActionState.Pending);
        command.Parameters.AddWithValue("$inFlight", (int)ProviderActionState.InFlight);
        command.Parameters.AddWithValue("$accepted", (int)ProviderActionState.Accepted);
        command.Parameters.AddWithValue("$retry", (int)ProviderActionState.RetryScheduled);
        command.Parameters.AddWithValue("$authBlocked", (int)ProviderActionState.AuthBlocked);
        command.Parameters.AddWithValue("$permanentFailure", (int)ProviderActionState.PermanentFailure);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);
        return new QueueSummary(
            reader.GetInt32(0),
            reader.GetInt32(1),
            reader.GetInt32(2),
            reader.GetInt32(3),
            reader.GetInt32(4),
            reader.GetInt32(5));
    }


    public async Task<IReadOnlyList<QueueActionSummary>> GetRecentActionsAsync(int limit, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1);
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT a.action_id, a.play_id, a.provider, a.action_type, a.state, a.attempt_count,
                   a.due_utc, a.last_reason_code, p.artist, p.title
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
                reader.IsDBNull(9) ? null : reader.GetString(9)));
        }

        return result;
    }
}
