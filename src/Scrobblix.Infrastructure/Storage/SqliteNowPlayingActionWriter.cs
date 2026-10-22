using System.Globalization;
using Microsoft.Data.Sqlite;
using Scrobblix.Domain.Queue;

namespace Scrobblix.Infrastructure.Storage;

/// <summary>
/// Persists a latest-wins Last.fm now-playing update. Unlike scrobbles, a now-playing
/// update is provisional metadata and stale pending updates should not be sent after
/// a newer track has started.
/// </summary>
public sealed class SqliteNowPlayingActionWriter(string connectionString)
{
    public async Task<bool> TryQueueAsync(
        string playId,
        string provider,
        string accountId,
        DateTimeOffset queuedUtc,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(playId);
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(accountId);

        var now = queuedUtc.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);
        var idempotencyKey = ProviderActionIdempotencyKey.Create(
            provider, accountId, ProviderActionType.NowPlaying, playId);

        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);

        await using (var play = connection.CreateCommand())
        {
            play.Transaction = transaction;
            play.CommandText = "SELECT artist, title FROM plays WHERE play_id = $playId;";
            play.Parameters.AddWithValue("$playId", playId);
            await using var reader = await play.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken)
                || reader.IsDBNull(0)
                || reader.IsDBNull(1)
                || string.IsNullOrWhiteSpace(reader.GetString(0))
                || string.IsNullOrWhiteSpace(reader.GetString(1)))
            {
                await transaction.CommitAsync(cancellationToken);
                return false;
            }
        }

        await using (var existing = connection.CreateCommand())
        {
            existing.Transaction = transaction;
            existing.CommandText = """
                SELECT 1 FROM provider_actions
                WHERE provider = $provider AND account_id = $accountId
                  AND action_type = $nowPlaying AND idempotency_key = $key
                LIMIT 1;
                """;
            existing.Parameters.AddWithValue("$provider", provider);
            existing.Parameters.AddWithValue("$accountId", accountId);
            existing.Parameters.AddWithValue("$nowPlaying", (int)ProviderActionType.NowPlaying);
            existing.Parameters.AddWithValue("$key", idempotencyKey);
            if (await existing.ExecuteScalarAsync(cancellationToken) is not null)
            {
                await transaction.CommitAsync(cancellationToken);
                return false;
            }
        }

        await using (var supersede = connection.CreateCommand())
        {
            supersede.Transaction = transaction;
            supersede.CommandText = """
                UPDATE provider_actions
                SET state = $cancelled,
                    last_reason_code = 'nowplaying.superseded',
                    completed_utc = $now,
                    lease_owner = NULL,
                    lease_expires_utc = NULL,
                    updated_utc = $now
                WHERE provider = $provider
                  AND account_id = $accountId
                  AND action_type = $nowPlaying
                  AND play_id <> $playId
                  AND state IN ($pending, $retry, $authBlocked, $retryExhausted, $outcomeUnknown);
                """;
            supersede.Parameters.AddWithValue("$cancelled", (int)ProviderActionState.Cancelled);
            supersede.Parameters.AddWithValue("$now", now);
            supersede.Parameters.AddWithValue("$provider", provider);
            supersede.Parameters.AddWithValue("$accountId", accountId);
            supersede.Parameters.AddWithValue("$nowPlaying", (int)ProviderActionType.NowPlaying);
            supersede.Parameters.AddWithValue("$playId", playId);
            supersede.Parameters.AddWithValue("$pending", (int)ProviderActionState.Pending);
            supersede.Parameters.AddWithValue("$retry", (int)ProviderActionState.RetryScheduled);
            supersede.Parameters.AddWithValue("$authBlocked", (int)ProviderActionState.AuthBlocked);
            supersede.Parameters.AddWithValue("$retryExhausted", (int)ProviderActionState.RetryExhausted);
            supersede.Parameters.AddWithValue("$outcomeUnknown", (int)ProviderActionState.SubmissionOutcomeUnknown);
            await supersede.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var insert = connection.CreateCommand())
        {
            insert.Transaction = transaction;
            insert.CommandText = """
                INSERT OR IGNORE INTO provider_actions (
                    action_id, play_id, account_id, provider, action_type, state,
                    idempotency_key, due_utc, created_utc, updated_utc)
                VALUES (
                    $actionId, $playId, $accountId, $provider, $nowPlaying, $pending,
                    $key, $now, $now, $now);
                """;
            insert.Parameters.AddWithValue("$actionId", Guid.NewGuid().ToString("N"));
            insert.Parameters.AddWithValue("$playId", playId);
            insert.Parameters.AddWithValue("$accountId", accountId);
            insert.Parameters.AddWithValue("$provider", provider);
            insert.Parameters.AddWithValue("$nowPlaying", (int)ProviderActionType.NowPlaying);
            insert.Parameters.AddWithValue("$pending", (int)ProviderActionState.Pending);
            insert.Parameters.AddWithValue("$key", idempotencyKey);
            insert.Parameters.AddWithValue("$now", now);
            var inserted = await insert.ExecuteNonQueryAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return inserted == 1;
        }
    }
}
