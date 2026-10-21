using System.Globalization;
using Microsoft.Data.Sqlite;
using Scrobblix.Application.Abstractions;
using Scrobblix.Domain.History;

namespace Scrobblix.Infrastructure.Storage;

public sealed class SqliteLocalPlayStore(string connectionString) : ILocalPlayStore
{
    public Task UpsertEligibleAsync(LocalPlayRecord play, CancellationToken cancellationToken)
    {
        if (!play.Eligibility.IsEligible)
        {
            throw new ArgumentException("Only eligible local plays can be stored.", nameof(play));
        }

        return UpsertAsync(play, cancellationToken);
    }

    public async Task UpsertAsync(LocalPlayRecord play, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(play.PlayId);
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();

        var observed = play.LastObservedUtc.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);
        var eligibleUtc = play.Eligibility.IsEligible ? observed : null;
        command.CommandText = """
            INSERT INTO plays (
                play_id, started_utc, artist, title, album, duration_ms,
                confirmed_played_ms, eligibility_threshold_ms, eligible_utc,
                local_status, terminal_reason_code, artwork_path, created_utc, updated_utc)
            VALUES (
                $playId, $startedUtc, $artist, $title, $album, $durationMs,
                $confirmedMs, $thresholdMs, $eligibleUtc,
                0, NULL, $artworkPath, $updatedUtc, $updatedUtc)
            ON CONFLICT(play_id) DO UPDATE SET
                artist = COALESCE(excluded.artist, plays.artist),
                title = COALESCE(excluded.title, plays.title),
                album = COALESCE(excluded.album, plays.album),
                duration_ms = COALESCE(excluded.duration_ms, plays.duration_ms),
                confirmed_played_ms = excluded.confirmed_played_ms,
                eligibility_threshold_ms = COALESCE(excluded.eligibility_threshold_ms, plays.eligibility_threshold_ms),
                eligible_utc = COALESCE(plays.eligible_utc, excluded.eligible_utc),
                terminal_reason_code = CASE
                    WHEN COALESCE(plays.eligible_utc, excluded.eligible_utc) IS NOT NULL THEN NULL
                    ELSE plays.terminal_reason_code
                END,
                artwork_path = COALESCE(excluded.artwork_path, plays.artwork_path),
                updated_utc = excluded.updated_utc;
            """;
        command.Parameters.AddWithValue("$playId", play.PlayId);
        command.Parameters.AddWithValue("$startedUtc", play.FirstObservedUtc.UtcDateTime.ToString("O", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$artist", (object?)play.Artist ?? DBNull.Value);
        command.Parameters.AddWithValue("$title", (object?)play.Title ?? DBNull.Value);
        command.Parameters.AddWithValue("$album", (object?)play.Album ?? DBNull.Value);
        command.Parameters.AddWithValue("$durationMs", (object?)play.DurationMilliseconds ?? DBNull.Value);
        command.Parameters.AddWithValue("$confirmedMs", play.ConfirmedPlayedMilliseconds);
        command.Parameters.AddWithValue("$thresholdMs", (object?)play.Eligibility.ThresholdMilliseconds ?? DBNull.Value);
        command.Parameters.AddWithValue("$eligibleUtc", (object?)eligibleUtc ?? DBNull.Value);
        command.Parameters.AddWithValue("$artworkPath", (object?)play.ArtworkPath ?? DBNull.Value);
        command.Parameters.AddWithValue("$updatedUtc", observed);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
