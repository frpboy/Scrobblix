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
}
