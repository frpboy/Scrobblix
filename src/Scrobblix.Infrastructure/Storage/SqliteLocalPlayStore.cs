using System.Globalization;
using Microsoft.Data.Sqlite;
using Scrobblix.Application.Abstractions;
using Scrobblix.Domain.History;

namespace Scrobblix.Infrastructure.Storage;

public sealed class SqliteLocalPlayStore(string connectionString) : ILocalPlayStore
{
    public async Task UpsertEligibleAsync(LocalPlayRecord play, CancellationToken cancellationToken)
    {
        if (!play.Eligibility.IsEligible)
        {
            throw new ArgumentException("Only eligible local plays can be stored.", nameof(play));
        }

        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        var observed = play.LastObservedUtc.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);
        command.CommandText = """
            INSERT INTO plays (play_id, started_utc, artist, title, album, duration_ms, confirmed_played_ms, eligibility_threshold_ms, eligible_utc, local_status, terminal_reason_code, created_utc, updated_utc)
            VALUES ($playId, $startedUtc, $artist, $title, $album, $durationMs, $confirmedMs, $thresholdMs, $eligibleUtc, 0, $reason, $updatedUtc, $updatedUtc)
            ON CONFLICT(play_id) DO UPDATE SET
                confirmed_played_ms = excluded.confirmed_played_ms,
                eligible_utc = excluded.eligible_utc,
                updated_utc = excluded.updated_utc;
            """;
        command.Parameters.AddWithValue("$playId", play.PlayId);
        command.Parameters.AddWithValue("$startedUtc", play.FirstObservedUtc.UtcDateTime.ToString("O", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$artist", (object?)play.Artist ?? DBNull.Value);
        command.Parameters.AddWithValue("$title", (object?)play.Title ?? DBNull.Value);
        command.Parameters.AddWithValue("$album", (object?)play.Album ?? DBNull.Value);
        command.Parameters.AddWithValue("$durationMs", (object?)play.DurationMilliseconds ?? DBNull.Value);
        command.Parameters.AddWithValue("$confirmedMs", play.ConfirmedPlayedMilliseconds);
        command.Parameters.AddWithValue("$thresholdMs", play.Eligibility.ThresholdMilliseconds!.Value);
        command.Parameters.AddWithValue("$eligibleUtc", observed);
        command.Parameters.AddWithValue("$reason", (object?)play.LocalReasonCode ?? DBNull.Value);
        command.Parameters.AddWithValue("$updatedUtc", observed);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
