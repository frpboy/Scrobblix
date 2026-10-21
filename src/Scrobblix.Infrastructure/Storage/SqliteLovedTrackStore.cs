using System.Globalization;
using Microsoft.Data.Sqlite;
using Scrobblix.Application.ReadModels;
using Scrobblix.Domain.Diagnostics;
using Scrobblix.Domain.Metadata;
using Scrobblix.Domain.Queue;

namespace Scrobblix.Infrastructure.Storage;

/// <summary>
/// Persists local love/unlove intent separately from Last.fm confirmation.
/// A failed provider request never erases the user's desired local state.
/// </summary>
public sealed class SqliteLovedTrackStore(string connectionString)
{
    public async Task<LovedTrackMutationResult> SetLovedAsync(
        string playId,
        bool isLoved,
        string? provider,
        string? accountId,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(playId);
        if (string.IsNullOrWhiteSpace(provider) != string.IsNullOrWhiteSpace(accountId))
        {
            throw new ArgumentException("Provider and account identifiers must both be supplied or both be omitted.");
        }

        var now = FormatUtc(nowUtc);
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);

        string? artist;
        string? title;
        string? album;
        await using (var playCommand = connection.CreateCommand())
        {
            playCommand.Transaction = transaction;
            playCommand.CommandText = """
                SELECT artist, title, album
                FROM plays
                WHERE play_id = $playId;
                """;
            playCommand.Parameters.AddWithValue("$playId", playId);
            await using var reader = await playCommand.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                throw new KeyNotFoundException($"Local play '{playId}' could not be found.");
            }

            artist = reader.IsDBNull(0) ? null : reader.GetString(0);
            title = reader.IsDBNull(1) ? null : reader.GetString(1);
            album = reader.IsDBNull(2) ? null : reader.GetString(2);
        }

        if (string.IsNullOrWhiteSpace(artist) || string.IsNullOrWhiteSpace(title))
        {
            await transaction.CommitAsync(cancellationToken);
            return new LovedTrackMutationResult(
                string.Empty,
                isLoved,
                false,
                false,
                "This track does not have enough artist/title metadata to identify it safely.");
        }

        var trackKey = TrackKey.Create(artist, title);
        bool? previousLocalState;
        bool? providerLoved;
        await using (var stateCommand = connection.CreateCommand())
        {
            stateCommand.Transaction = transaction;
            stateCommand.CommandText = """
                SELECT is_loved, provider_loved
                FROM loved_tracks
                WHERE track_key = $trackKey;
                """;
            stateCommand.Parameters.AddWithValue("$trackKey", trackKey);
            await using var reader = await stateCommand.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                previousLocalState = reader.GetInt32(0) == 1;
                providerLoved = reader.IsDBNull(1) ? null : reader.GetInt32(1) == 1;
            }
            else
            {
                previousLocalState = null;
                providerLoved = null;
            }
        }

        await using (var upsert = connection.CreateCommand())
        {
            upsert.Transaction = transaction;
            upsert.CommandText = """
                INSERT INTO loved_tracks (
                    track_key, source_play_id, artist, title, album, is_loved,
                    provider_loved, current_action_id, created_utc, updated_utc)
                VALUES (
                    $trackKey, $playId, $artist, $title, $album, $isLoved,
                    NULL, NULL, $now, $now)
                ON CONFLICT(track_key) DO UPDATE SET
                    source_play_id = excluded.source_play_id,
                    artist = excluded.artist,
                    title = excluded.title,
                    album = COALESCE(excluded.album, loved_tracks.album),
                    is_loved = excluded.is_loved,
                    updated_utc = excluded.updated_utc;
                """;
            upsert.Parameters.AddWithValue("$trackKey", trackKey);
            upsert.Parameters.AddWithValue("$playId", playId);
            upsert.Parameters.AddWithValue("$artist", artist);
            upsert.Parameters.AddWithValue("$title", title);
            upsert.Parameters.AddWithValue("$album", (object?)album ?? DBNull.Value);
            upsert.Parameters.AddWithValue("$isLoved", isLoved ? 1 : 0);
            upsert.Parameters.AddWithValue("$now", now);
            await upsert.ExecuteNonQueryAsync(cancellationToken);
        }

        var targetAction = isLoved ? ProviderActionType.Love : ProviderActionType.Unlove;
        string? actionId = null;
        var actionQueued = false;
        string? reason = null;
        if (!string.IsNullOrWhiteSpace(provider) && !string.IsNullOrWhiteSpace(accountId))
        {
            var queued = await EnsureActionQueuedAsync(
                connection,
                transaction,
                trackKey,
                playId,
                artist,
                title,
                album,
                isLoved,
                providerLoved,
                provider,
                accountId,
                now,
                allowRetryAfterFailure: true,
                cancellationToken);
            actionId = queued.ActionId;
            actionQueued = queued.HasPendingAction;
            reason = queued.Reason;
        }
        else
        {
            await CancelOppositePendingActionsAsync(
                connection, transaction, trackKey, targetAction, now, cancellationToken);
            actionId = await FindActionIdAsync(
                connection,
                transaction,
                trackKey,
                targetAction,
                new[] { ProviderActionState.Pending, ProviderActionState.InFlight, ProviderActionState.RetryScheduled, ProviderActionState.AuthBlocked, ProviderActionState.PermanentFailure },
                cancellationToken);
            actionQueued = actionId is not null;
            reason = isLoved
                ? "Saved locally. Connect Last.fm to sync this love state."
                : "Unlove saved locally. Connect Last.fm to sync the removal.";
        }

        await using (var update = connection.CreateCommand())
        {
            update.Transaction = transaction;
            update.CommandText = """
                UPDATE loved_tracks
                SET current_action_id = $actionId, updated_utc = $now
                WHERE track_key = $trackKey;
                """;
            update.Parameters.AddWithValue("$actionId", (object?)actionId ?? DBNull.Value);
            update.Parameters.AddWithValue("$now", now);
            update.Parameters.AddWithValue("$trackKey", trackKey);
            await update.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return new LovedTrackMutationResult(
            trackKey,
            isLoved,
            previousLocalState is null || previousLocalState.Value != isLoved,
            actionQueued,
            reason);
    }

    public async Task<int> QueuePendingSyncAsync(
        string provider,
        string accountId,
        DateTimeOffset nowUtc,
        bool retryBlockedActions,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(accountId);
        var now = FormatUtc(nowUtc);
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);

        var tracks = new List<LocalLovedTrackState>();
        await using (var select = connection.CreateCommand())
        {
            select.Transaction = transaction;
            select.CommandText = """
                SELECT track_key, source_play_id, artist, title, album, is_loved, provider_loved
                FROM loved_tracks
                WHERE provider_loved IS NULL OR provider_loved <> is_loved
                ORDER BY updated_utc ASC, track_key ASC;
                """;
            await using var reader = await select.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                tracks.Add(new LocalLovedTrackState(
                    reader.GetString(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetString(3),
                    reader.IsDBNull(4) ? null : reader.GetString(4),
                    reader.GetInt32(5) == 1,
                    reader.IsDBNull(6) ? null : reader.GetInt32(6) == 1));
            }
        }

        var queuedCount = 0;
        foreach (var track in tracks)
        {
            var result = await EnsureActionQueuedAsync(
                connection,
                transaction,
                track.TrackKey,
                track.SourcePlayId,
                track.Artist,
                track.Title,
                track.Album,
                track.IsLoved,
                track.ProviderLoved,
                provider,
                accountId,
                now,
                retryBlockedActions,
                cancellationToken);
            if (result.Created)
            {
                queuedCount++;
            }

            await using var update = connection.CreateCommand();
            update.Transaction = transaction;
            update.CommandText = """
                UPDATE loved_tracks
                SET current_action_id = $actionId, updated_utc = $now
                WHERE track_key = $trackKey;
                """;
            update.Parameters.AddWithValue("$actionId", (object?)result.ActionId ?? DBNull.Value);
            update.Parameters.AddWithValue("$now", now);
            update.Parameters.AddWithValue("$trackKey", track.TrackKey);
            await update.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return queuedCount;
    }

    public async Task<IReadOnlyList<LovedTrackItem>> GetLovedTracksAsync(int limit, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1);
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT l.track_key, l.source_play_id, l.artist, l.title, l.album, l.is_loved,
                   l.provider_loved, l.current_action_id, a.action_type, a.state,
                   a.last_reason_code, l.updated_utc
            FROM loved_tracks l
            LEFT JOIN provider_actions a ON a.action_id = l.current_action_id
            WHERE l.is_loved = 1
            ORDER BY l.updated_utc DESC, l.artist COLLATE NOCASE, l.title COLLATE NOCASE
            LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$limit", limit);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var items = new List<LovedTrackItem>();
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(new LovedTrackItem(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetString(4),
                reader.GetInt32(5) == 1,
                reader.IsDBNull(6) ? null : reader.GetInt32(6) == 1,
                reader.IsDBNull(7) ? null : reader.GetString(7),
                reader.IsDBNull(8) ? null : (ProviderActionType?)reader.GetInt32(8),
                reader.IsDBNull(9) ? null : (ProviderActionState?)reader.GetInt32(9),
                reader.IsDBNull(10) ? null : reader.GetString(10),
                DateTimeOffset.Parse(reader.GetString(11), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)));
        }

        return items;
    }

    private static async Task<int> CancelOppositePendingActionsAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string trackKey,
        ProviderActionType targetAction,
        string now,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            UPDATE provider_actions
            SET state = $cancelled, last_reason_code = $reason, completed_utc = $now,
                lease_owner = NULL, lease_expires_utc = NULL, updated_utc = $now
            WHERE track_key = $trackKey
              AND action_type <> $targetAction
              AND state IN ($pending, $retry);
            """;
        command.Parameters.AddWithValue("$cancelled", (int)ProviderActionState.Cancelled);
        command.Parameters.AddWithValue("$reason", ReasonCodes.Provider.ActionSuperseded);
        command.Parameters.AddWithValue("$now", now);
        command.Parameters.AddWithValue("$trackKey", trackKey);
        command.Parameters.AddWithValue("$targetAction", (int)targetAction);
        command.Parameters.AddWithValue("$pending", (int)ProviderActionState.Pending);
        command.Parameters.AddWithValue("$retry", (int)ProviderActionState.RetryScheduled);
        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<QueueActionResult> EnsureActionQueuedAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string trackKey,
        string playId,
        string artist,
        string title,
        string? album,
        bool desiredLoved,
        bool? providerLoved,
        string provider,
        string accountId,
        string now,
        bool allowRetryAfterFailure,
        CancellationToken cancellationToken)
    {
        var targetAction = desiredLoved ? ProviderActionType.Love : ProviderActionType.Unlove;
        await CancelOppositePendingActionsAsync(connection, transaction, trackKey, targetAction, now, cancellationToken);

        var activeTargetId = await FindActionIdAsync(
            connection,
            transaction,
            trackKey,
            targetAction,
            new[] { ProviderActionState.Pending, ProviderActionState.InFlight, ProviderActionState.RetryScheduled },
            cancellationToken);
        if (activeTargetId is not null)
        {
            return new QueueActionResult(activeTargetId, true, false, "A matching Last.fm action is already queued.");
        }

        var blockedTargetId = await FindActionIdAsync(
            connection,
            transaction,
            trackKey,
            targetAction,
            new[] { ProviderActionState.AuthBlocked, ProviderActionState.PermanentFailure },
            cancellationToken);
        if (blockedTargetId is not null && !allowRetryAfterFailure)
        {
            return new QueueActionResult(blockedTargetId, false, false, "The last matching action is blocked or failed. Reconnect Last.fm or explicitly retry this track.");
        }

        var activeOppositeId = await FindActionIdAsync(
            connection,
            transaction,
            trackKey,
            desiredLoved ? ProviderActionType.Unlove : ProviderActionType.Love,
            new[] { ProviderActionState.InFlight },
            cancellationToken);
        var remoteMatches = providerLoved is not null && providerLoved.Value == desiredLoved;
        if (remoteMatches && activeOppositeId is null && blockedTargetId is null)
        {
            return new QueueActionResult(null, false, false, "Last.fm is already in the requested state.");
        }

        var actionId = Guid.NewGuid().ToString("N");
        await using (var insert = connection.CreateCommand())
        {
            insert.Transaction = transaction;
            insert.CommandText = """
                INSERT INTO provider_actions (
                    action_id, play_id, account_id, provider, action_type, state,
                    idempotency_key, due_utc, created_utc, updated_utc, track_key)
                VALUES (
                    $actionId, $playId, $accountId, $provider, $actionType, $state,
                    $idempotencyKey, $now, $now, $now, $trackKey);
                """;
            insert.Parameters.AddWithValue("$actionId", actionId);
            insert.Parameters.AddWithValue("$playId", playId);
            insert.Parameters.AddWithValue("$accountId", accountId);
            insert.Parameters.AddWithValue("$provider", provider);
            insert.Parameters.AddWithValue("$actionType", (int)targetAction);
            insert.Parameters.AddWithValue("$state", (int)ProviderActionState.Pending);
            insert.Parameters.AddWithValue("$idempotencyKey",
                ProviderActionIdempotencyKey.Create(provider, accountId, targetAction, actionId));
            insert.Parameters.AddWithValue("$now", now);
            insert.Parameters.AddWithValue("$trackKey", trackKey);
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }

        return new QueueActionResult(actionId, true, true, null);
    }

    private static async Task<string?> FindActionIdAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string trackKey,
        ProviderActionType actionType,
        IReadOnlyList<ProviderActionState> states,
        CancellationToken cancellationToken)
    {
        if (states.Count == 0)
        {
            return null;
        }

        var stateParameters = states.Select((_, index) => "$state" + index.ToString(CultureInfo.InvariantCulture)).ToArray();
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"""
            SELECT action_id
            FROM provider_actions
            WHERE track_key = $trackKey AND action_type = $actionType
              AND state IN ({string.Join(", ", stateParameters)})
            ORDER BY created_utc DESC
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$trackKey", trackKey);
        command.Parameters.AddWithValue("$actionType", (int)actionType);
        for (var index = 0; index < states.Count; index++)
        {
            command.Parameters.AddWithValue(stateParameters[index], (int)states[index]);
        }

        return await command.ExecuteScalarAsync(cancellationToken) as string;
    }

    private static string FormatUtc(DateTimeOffset value) =>
        value.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);

    private sealed record LocalLovedTrackState(
        string TrackKey,
        string SourcePlayId,
        string Artist,
        string Title,
        string? Album,
        bool IsLoved,
        bool? ProviderLoved);

    private sealed record QueueActionResult(
        string? ActionId,
        bool HasPendingAction,
        bool Created,
        string? Reason);
}
