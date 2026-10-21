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
        await using (var playCommand = connection.CreateCommand())
        {
            playCommand.Transaction = transaction;
            playCommand.CommandText = """
                INSERT INTO plays (play_id, started_utc, artist, title, album, duration_ms, confirmed_played_ms, eligibility_threshold_ms, eligible_utc, local_status, artwork_path, created_utc, updated_utc)
                VALUES ($playId, $startedUtc, $artist, $title, $album, $durationMs, $confirmedMs, $thresholdMs, $queuedUtc, 0, $artworkPath, $queuedUtc, $queuedUtc)
                ON CONFLICT(play_id) DO UPDATE SET
                    artist = COALESCE(excluded.artist, plays.artist),
                    title = COALESCE(excluded.title, plays.title),
                    album = COALESCE(excluded.album, plays.album),
                    duration_ms = COALESCE(excluded.duration_ms, plays.duration_ms),
                    confirmed_played_ms = excluded.confirmed_played_ms,
                    eligibility_threshold_ms = COALESCE(excluded.eligibility_threshold_ms, plays.eligibility_threshold_ms),
                    eligible_utc = COALESCE(plays.eligible_utc, excluded.eligible_utc),
                    terminal_reason_code = NULL,
                    artwork_path = COALESCE(excluded.artwork_path, plays.artwork_path),
                    updated_utc = excluded.updated_utc;
                """;
            playCommand.Parameters.AddWithValue("$playId", play.PlayId);
            playCommand.Parameters.AddWithValue("$startedUtc", play.FirstObservedUtc.UtcDateTime.ToString("O", System.Globalization.CultureInfo.InvariantCulture));
            playCommand.Parameters.AddWithValue("$artist", (object?)play.Artist ?? DBNull.Value);
            playCommand.Parameters.AddWithValue("$title", (object?)play.Title ?? DBNull.Value);
            playCommand.Parameters.AddWithValue("$album", (object?)play.Album ?? DBNull.Value);
            playCommand.Parameters.AddWithValue("$durationMs", (object?)play.DurationMilliseconds ?? DBNull.Value);
            playCommand.Parameters.AddWithValue("$confirmedMs", play.ConfirmedPlayedMilliseconds);
            playCommand.Parameters.AddWithValue("$thresholdMs", play.Eligibility.ThresholdMilliseconds!.Value);
            playCommand.Parameters.AddWithValue("$queuedUtc", timestamp);
            playCommand.Parameters.AddWithValue("$artworkPath", (object?)play.ArtworkPath ?? DBNull.Value);
            await playCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await using var actionCommand = connection.CreateCommand();
        actionCommand.Transaction = transaction;
        actionCommand.CommandText = """
            INSERT OR IGNORE INTO provider_actions (action_id, play_id, account_id, provider, action_type, state, idempotency_key, due_utc, created_utc, updated_utc)
            VALUES ($actionId, $playId, $accountId, $provider, $actionType, $state, $idempotencyKey, $queuedUtc, $queuedUtc, $queuedUtc);
            """;
        actionCommand.Parameters.AddWithValue("$playId", play.PlayId);
        actionCommand.Parameters.AddWithValue("$queuedUtc", timestamp);
        actionCommand.Parameters.AddWithValue("$actionId", Guid.NewGuid().ToString("N"));
        actionCommand.Parameters.AddWithValue("$accountId", accountId);
        actionCommand.Parameters.AddWithValue("$provider", provider);
        actionCommand.Parameters.AddWithValue("$actionType", (int)ProviderActionType.Scrobble);
        actionCommand.Parameters.AddWithValue("$state", (int)ProviderActionState.Pending);
        actionCommand.Parameters.AddWithValue("$idempotencyKey", actionKey);
        var actionInserted = await actionCommand.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return actionInserted == 1;
    }
}
