using Microsoft.Data.Sqlite;
using Scrobblix.Application.Abstractions;
using Scrobblix.Domain.History;
using Scrobblix.Domain.Queue;

namespace Scrobblix.Infrastructure.Storage;

public sealed class SqliteEligiblePlayStore(string connectionString) : IEligiblePlayStore
{
    public async Task<bool> TryPersistAndQueueAsync(LocalPlayRecord play, string provider, string accountId, DateTimeOffset queuedUtc, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(accountId);
        if (!play.Eligibility.IsEligible)
        {
            throw new ArgumentException("Only eligible plays can be queued.", nameof(play));
        }

        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        var actionKey = ProviderActionIdempotencyKey.Create(provider, accountId, ProviderActionType.Scrobble, play.PlayId);
        var timestamp = queuedUtc.UtcDateTime.ToString("O", System.Globalization.CultureInfo.InvariantCulture);
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT OR IGNORE INTO plays (play_id, started_utc, artist, title, album, duration_ms, confirmed_played_ms, eligibility_threshold_ms, eligible_utc, local_status, created_utc, updated_utc)
            VALUES ($playId, $startedUtc, $artist, $title, $album, $durationMs, $confirmedMs, $thresholdMs, $queuedUtc, 0, $queuedUtc, $queuedUtc);
            INSERT OR IGNORE INTO provider_actions (action_id, play_id, account_id, provider, action_type, state, idempotency_key, due_utc, created_utc, updated_utc)
            VALUES ($actionId, $playId, $accountId, $provider, $actionType, $state, $idempotencyKey, $queuedUtc, $queuedUtc, $queuedUtc);
            """;
        command.Parameters.AddWithValue("$playId", play.PlayId);
        command.Parameters.AddWithValue("$startedUtc", play.FirstObservedUtc.UtcDateTime.ToString("O", System.Globalization.CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$artist", (object?)play.Artist ?? DBNull.Value);
        command.Parameters.AddWithValue("$title", (object?)play.Title ?? DBNull.Value);
        command.Parameters.AddWithValue("$album", (object?)play.Album ?? DBNull.Value);
        command.Parameters.AddWithValue("$durationMs", (object?)play.DurationMilliseconds ?? DBNull.Value);
        command.Parameters.AddWithValue("$confirmedMs", play.ConfirmedPlayedMilliseconds);
        command.Parameters.AddWithValue("$thresholdMs", play.Eligibility.ThresholdMilliseconds!.Value);
        command.Parameters.AddWithValue("$queuedUtc", timestamp);
        command.Parameters.AddWithValue("$actionId", Guid.NewGuid().ToString("N"));
        command.Parameters.AddWithValue("$accountId", accountId);
        command.Parameters.AddWithValue("$provider", provider);
        command.Parameters.AddWithValue("$actionType", (int)ProviderActionType.Scrobble);
        command.Parameters.AddWithValue("$state", (int)ProviderActionState.Pending);
        command.Parameters.AddWithValue("$idempotencyKey", actionKey);
        var affectedRows = await command.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return affectedRows == 2;
    }
}
