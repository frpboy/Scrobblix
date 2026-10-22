using System.Globalization;
using Microsoft.Data.Sqlite;
using Scrobblix.Application.Abstractions;
using Scrobblix.Domain.History;
using Scrobblix.Domain.Metadata;

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
                local_status, terminal_reason_code, artwork_path, album_artist,
                track_number, release_year, recording_mbid, release_mbid, artwork_source,
                created_utc, updated_utc)
            VALUES (
                $playId, $startedUtc, $artist, $title, $album, $durationMs,
                $confirmedMs, $thresholdMs, $eligibleUtc,
                0, NULL, $artworkPath, $albumArtist,
                $trackNumber, $releaseYear, $recordingMbid, $releaseMbid, $artworkSource,
                $updatedUtc, $updatedUtc)
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
                album_artist = COALESCE(excluded.album_artist, plays.album_artist),
                track_number = COALESCE(excluded.track_number, plays.track_number),
                release_year = COALESCE(excluded.release_year, plays.release_year),
                recording_mbid = COALESCE(excluded.recording_mbid, plays.recording_mbid),
                release_mbid = COALESCE(excluded.release_mbid, plays.release_mbid),
                artwork_source = COALESCE(excluded.artwork_source, plays.artwork_source),
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
        command.Parameters.AddWithValue("$albumArtist", (object?)play.AlbumArtist ?? DBNull.Value);
        command.Parameters.AddWithValue("$trackNumber", (object?)play.TrackNumber ?? DBNull.Value);
        command.Parameters.AddWithValue("$releaseYear", (object?)play.ReleaseYear ?? DBNull.Value);
        command.Parameters.AddWithValue("$recordingMbid", (object?)play.RecordingMusicBrainzId ?? DBNull.Value);
        command.Parameters.AddWithValue("$releaseMbid", (object?)play.ReleaseMusicBrainzId ?? DBNull.Value);
        command.Parameters.AddWithValue("$artworkSource", (object?)play.ArtworkSource ?? DBNull.Value);
        command.Parameters.AddWithValue("$updatedUtc", observed);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task UpdateMetadataAsync(string playId, PlayMetadataEnrichment metadata, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(playId);
        ArgumentNullException.ThrowIfNull(metadata);
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE plays SET
                album = COALESCE($album, album),
                album_artist = COALESCE($albumArtist, album_artist),
                track_number = COALESCE($trackNumber, track_number),
                release_year = COALESCE($releaseYear, release_year),
                recording_mbid = COALESCE($recordingMbid, recording_mbid),
                release_mbid = COALESCE($releaseMbid, release_mbid),
                artwork_path = COALESCE($artworkPath, artwork_path),
                artwork_source = COALESCE($artworkSource, artwork_source),
                updated_utc = $updatedUtc
            WHERE play_id = $playId;
            """;
        command.Parameters.AddWithValue("$playId", playId);
        command.Parameters.AddWithValue("$album", (object?)metadata.Album ?? DBNull.Value);
        command.Parameters.AddWithValue("$albumArtist", (object?)metadata.AlbumArtist ?? DBNull.Value);
        command.Parameters.AddWithValue("$trackNumber", (object?)metadata.TrackNumber ?? DBNull.Value);
        command.Parameters.AddWithValue("$releaseYear", (object?)metadata.ReleaseYear ?? DBNull.Value);
        command.Parameters.AddWithValue("$recordingMbid", (object?)metadata.RecordingMusicBrainzId ?? DBNull.Value);
        command.Parameters.AddWithValue("$releaseMbid", (object?)metadata.ReleaseMusicBrainzId ?? DBNull.Value);
        command.Parameters.AddWithValue("$artworkPath", (object?)metadata.ArtworkPath ?? DBNull.Value);
        command.Parameters.AddWithValue("$artworkSource", (object?)metadata.ArtworkSource ?? DBNull.Value);
        command.Parameters.AddWithValue("$updatedUtc", DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
