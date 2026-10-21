using System.Globalization;
using Microsoft.Data.Sqlite;
using Scrobblix.Application.Abstractions;
using Scrobblix.Domain.Queue;

namespace Scrobblix.Infrastructure.Storage;

public sealed class SqliteProviderActionStore(string connectionString) : IProviderActionStore
{
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMinutes(1);

    public async Task<ProviderActionLease?> ClaimNextDueAsync(
        string workerId,
        DateTimeOffset nowUtc,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workerId);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(leaseDuration, TimeSpan.Zero);

        var now = nowUtc.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);
        var expires = nowUtc.Add(leaseDuration).UtcDateTime.ToString("O", CultureInfo.InvariantCulture);
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);

        await using var select = connection.CreateCommand();
        select.Transaction = transaction;
        select.CommandText = """
            SELECT a.action_id, a.play_id, a.provider, a.account_id, p.artist, p.title, p.album, p.started_utc, p.duration_ms, a.attempt_count
            FROM provider_actions a
            JOIN plays p ON p.play_id = a.play_id
            WHERE a.state IN ($pending, $retry) AND a.due_utc <= $now
            ORDER BY a.due_utc, a.created_utc
            LIMIT 1;
            """;
        select.Parameters.AddWithValue("$pending", (int)ProviderActionState.Pending);
        select.Parameters.AddWithValue("$retry", (int)ProviderActionState.RetryScheduled);
        select.Parameters.AddWithValue("$now", now);
        await using var reader = await select.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            await transaction.CommitAsync(cancellationToken);
            return null;
        }

        var actionId = reader.GetString(0);
        var lease = new ProviderActionLease(
            actionId,
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.IsDBNull(4) ? null : reader.GetString(4),
            reader.IsDBNull(5) ? null : reader.GetString(5),
            reader.IsDBNull(6) ? null : reader.GetString(6),
            DateTimeOffset.Parse(reader.GetString(7), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
            reader.IsDBNull(8) ? null : reader.GetInt32(8),
            reader.GetInt32(9) + 1);
        await reader.DisposeAsync();

        await using var update = connection.CreateCommand();
        update.Transaction = transaction;
        update.CommandText = """
            UPDATE provider_actions
            SET state = $inFlight, attempt_count = attempt_count + 1, lease_owner = $owner,
                lease_expires_utc = $expires, updated_utc = $now
            WHERE action_id = $actionId AND state IN ($pending, $retry);
            """;
        update.Parameters.AddWithValue("$inFlight", (int)ProviderActionState.InFlight);
        update.Parameters.AddWithValue("$owner", workerId);
        update.Parameters.AddWithValue("$expires", expires);
        update.Parameters.AddWithValue("$now", now);
        update.Parameters.AddWithValue("$actionId", actionId);
        update.Parameters.AddWithValue("$pending", (int)ProviderActionState.Pending);
        update.Parameters.AddWithValue("$retry", (int)ProviderActionState.RetryScheduled);
        if (await update.ExecuteNonQueryAsync(cancellationToken) != 1)
        {
            throw new InvalidOperationException("Provider action could not be leased.");
        }

        await transaction.CommitAsync(cancellationToken);
        return lease;
    }

    public async Task CompleteAsync(
        string actionId,
        ProviderActionState outcome,
        string reasonCode,
        DateTimeOffset completedUtc,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(reasonCode);
        if (outcome is ProviderActionState.Pending or ProviderActionState.InFlight)
        {
            throw new ArgumentOutOfRangeException(nameof(outcome));
        }

        var completed = completedUtc.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);
        var retryDue = completedUtc.Add(RetryDelay).UtcDateTime.ToString("O", CultureInfo.InvariantCulture);
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE provider_actions
            SET state = $state, lease_owner = NULL, lease_expires_utc = NULL,
                last_reason_code = $reason, updated_utc = $completed,
                due_utc = CASE WHEN $state = $retry THEN $retryDue ELSE due_utc END,
                completed_utc = CASE WHEN $state = $retry THEN NULL ELSE $completed END
            WHERE action_id = $actionId AND state = $inFlight;
            """;
        command.Parameters.AddWithValue("$state", (int)outcome);
        command.Parameters.AddWithValue("$reason", reasonCode);
        command.Parameters.AddWithValue("$completed", completed);
        command.Parameters.AddWithValue("$retry", (int)ProviderActionState.RetryScheduled);
        command.Parameters.AddWithValue("$retryDue", retryDue);
        command.Parameters.AddWithValue("$actionId", actionId);
        command.Parameters.AddWithValue("$inFlight", (int)ProviderActionState.InFlight);
        if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
        {
            throw new InvalidOperationException("Provider action completion was rejected.");
        }
    }
}
