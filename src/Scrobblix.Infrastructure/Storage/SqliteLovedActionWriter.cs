using Microsoft.Data.Sqlite;
using Scrobblix.Domain.Queue;

namespace Scrobblix.Infrastructure.Storage;

public sealed class SqliteLovedActionWriter(string connectionString)
{
    public async Task<bool> EnqueueAsync(string playId, string accountId, ProviderActionType actionType, DateTimeOffset requestedUtc, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(playId);
        ArgumentException.ThrowIfNullOrWhiteSpace(accountId);
        if (actionType is not (ProviderActionType.Love or ProviderActionType.Unlove))
            throw new ArgumentOutOfRangeException(nameof(actionType), "Only Love and Unlove are supported.");

        var now = requestedUtc.UtcDateTime.ToString("O", System.Globalization.CultureInfo.InvariantCulture);
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);

        await using (var playCheck = connection.CreateCommand())
        {
            playCheck.Transaction = transaction;
            playCheck.CommandText = """
                SELECT COUNT(*) FROM plays
                WHERE play_id = $playId AND TRIM(COALESCE(artist, '')) <> '' AND TRIM(COALESCE(title, '')) <> '';
                """;
            playCheck.Parameters.AddWithValue("$playId", playId);
            if (Convert.ToInt32(await playCheck.ExecuteScalarAsync(cancellationToken), System.Globalization.CultureInfo.InvariantCulture) != 1)
                throw new InvalidOperationException("The selected play has no usable artist/title or has not been persisted.");
        }

        await using (var latest = connection.CreateCommand())
        {
            latest.Transaction = transaction;
            latest.CommandText = """
                SELECT action_type, state
                FROM provider_actions
                WHERE provider = 'lastfm' AND account_id = $accountId AND play_id = $playId
                  AND action_type IN ($love, $unlove)
                ORDER BY created_utc DESC, action_id DESC
                LIMIT 1;
                """;
            latest.Parameters.AddWithValue("$accountId", accountId);
            latest.Parameters.AddWithValue("$playId", playId);
            latest.Parameters.AddWithValue("$love", (int)ProviderActionType.Love);
            latest.Parameters.AddWithValue("$unlove", (int)ProviderActionType.Unlove);
            await using var reader = await latest.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                var latestType = (ProviderActionType)reader.GetInt32(0);
                var latestState = (ProviderActionState)reader.GetInt32(1);
                if (latestType == actionType && latestState is ProviderActionState.Pending or ProviderActionState.InFlight or ProviderActionState.RetryScheduled or ProviderActionState.AuthBlocked or ProviderActionState.Accepted)
                {
                    await reader.DisposeAsync();
                    await transaction.CommitAsync(cancellationToken);
                    return false;
                }
            }
        }

        await using (var supersede = connection.CreateCommand())
        {
            supersede.Transaction = transaction;
            supersede.CommandText = """
                UPDATE provider_actions
                SET state = $cancelled, last_reason_code = $reason, completed_utc = $now, updated_utc = $now
                WHERE provider = 'lastfm' AND account_id = $accountId AND play_id = $playId
                  AND action_type IN ($love, $unlove) AND action_type <> $newType
                  AND state IN ($pending, $retry, $authBlocked);
                """;
            supersede.Parameters.AddWithValue("$cancelled", (int)ProviderActionState.Cancelled);
            supersede.Parameters.AddWithValue("$reason", "user.superseded_by_newer_love_intent");
            supersede.Parameters.AddWithValue("$now", now);
            supersede.Parameters.AddWithValue("$accountId", accountId);
            supersede.Parameters.AddWithValue("$playId", playId);
            supersede.Parameters.AddWithValue("$love", (int)ProviderActionType.Love);
            supersede.Parameters.AddWithValue("$unlove", (int)ProviderActionType.Unlove);
            supersede.Parameters.AddWithValue("$newType", (int)actionType);
            supersede.Parameters.AddWithValue("$pending", (int)ProviderActionState.Pending);
            supersede.Parameters.AddWithValue("$retry", (int)ProviderActionState.RetryScheduled);
            supersede.Parameters.AddWithValue("$authBlocked", (int)ProviderActionState.AuthBlocked);
            await supersede.ExecuteNonQueryAsync(cancellationToken);
        }

        var key = ProviderActionIdempotencyKey.Create("lastfm", accountId, actionType, playId) + ":intent:" + Guid.NewGuid().ToString("N");
        await using var insert = connection.CreateCommand();
        insert.Transaction = transaction;
        insert.CommandText = """
            INSERT INTO provider_actions (
                action_id, play_id, account_id, provider, action_type, state, idempotency_key,
                due_utc, created_utc, updated_utc)
            VALUES ($id, $playId, $accountId, 'lastfm', $type, $pending, $key, $now, $now, $now);
            """;
        insert.Parameters.AddWithValue("$id", Guid.NewGuid().ToString("N"));
        insert.Parameters.AddWithValue("$playId", playId);
        insert.Parameters.AddWithValue("$accountId", accountId);
        insert.Parameters.AddWithValue("$type", (int)actionType);
        insert.Parameters.AddWithValue("$pending", (int)ProviderActionState.Pending);
        insert.Parameters.AddWithValue("$key", key);
        insert.Parameters.AddWithValue("$now", now);
        var inserted = await insert.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return inserted == 1;
    }
}
