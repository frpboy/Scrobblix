using Microsoft.Data.Sqlite;
using Scrobblix.Application.Abstractions;
using Scrobblix.Domain.History;

namespace Scrobblix.Infrastructure.Storage;

public sealed class SqlitePlaybackObservationStore(string connectionString) : IPlaybackObservationStore
{
    public async Task<bool> TryAppendAsync(PlayObservation observation, CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT OR IGNORE INTO playback_observations (
                observation_id, source_instance_id, session_id, sequence_number, event_id,
                captured_utc, observed_utc, source_name, application_id, artist, title, album,
                duration_ms, position_ms, playback_state, normalized_artist, normalized_title,
                artwork_path, artwork_source, album_artist, track_number, release_year,
                recording_mbid, release_mbid)
            VALUES ($id, $sourceInstanceId, $sessionId, $sequenceNumber, $eventId,
                $capturedUtc, $observedUtc, $sourceName, $applicationId, $artist, $title, $album,
                $durationMilliseconds, $positionMilliseconds, $playbackState, $normalizedArtist, $normalizedTitle,
                $artworkPath, $artworkSource, $albumArtist, $trackNumber, $releaseYear,
                $recordingMbid, $releaseMbid);
            """;
        command.Parameters.AddWithValue("$id", Guid.NewGuid().ToString("N"));
        command.Parameters.AddWithValue("$sourceInstanceId", observation.SourceEventKey.SourceInstanceId);
        command.Parameters.AddWithValue("$sessionId", (object?)observation.SourceEventKey.SessionId ?? DBNull.Value);
        command.Parameters.AddWithValue("$sequenceNumber", observation.SourceEventKey.SequenceNumber);
        command.Parameters.AddWithValue("$eventId", (object?)observation.SourceEventKey.EventId ?? DBNull.Value);
        command.Parameters.AddWithValue("$capturedUtc", observation.CapturedAtUtc.UtcDateTime.ToString("O", System.Globalization.CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$observedUtc", observation.ObservedAtUtc.UtcDateTime.ToString("O", System.Globalization.CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$sourceName", observation.SourceName);
        command.Parameters.AddWithValue("$applicationId", (object?)observation.ApplicationId ?? DBNull.Value);
        command.Parameters.AddWithValue("$artist", (object?)observation.Artist ?? DBNull.Value);
        command.Parameters.AddWithValue("$title", (object?)observation.Title ?? DBNull.Value);
        command.Parameters.AddWithValue("$album", (object?)observation.Album ?? DBNull.Value);
        command.Parameters.AddWithValue("$durationMilliseconds", (object?)observation.DurationMilliseconds ?? DBNull.Value);
        command.Parameters.AddWithValue("$positionMilliseconds", (object?)observation.PositionMilliseconds ?? DBNull.Value);
        command.Parameters.AddWithValue("$playbackState", (int)observation.PlaybackState);
        command.Parameters.AddWithValue("$normalizedArtist", (object?)observation.NormalizedArtist?.Payload ?? DBNull.Value);
        command.Parameters.AddWithValue("$normalizedTitle", (object?)observation.NormalizedTitle?.Payload ?? DBNull.Value);
        command.Parameters.AddWithValue("$artworkPath", (object?)observation.ArtworkPath ?? DBNull.Value);
        command.Parameters.AddWithValue("$artworkSource", (object?)observation.ArtworkSource ?? DBNull.Value);
        command.Parameters.AddWithValue("$albumArtist", (object?)observation.AlbumArtist ?? DBNull.Value);
        command.Parameters.AddWithValue("$trackNumber", (object?)observation.TrackNumber ?? DBNull.Value);
        command.Parameters.AddWithValue("$releaseYear", (object?)observation.ReleaseYear ?? DBNull.Value);
        command.Parameters.AddWithValue("$recordingMbid", (object?)observation.RecordingMusicBrainzId ?? DBNull.Value);
        command.Parameters.AddWithValue("$releaseMbid", (object?)observation.ReleaseMusicBrainzId ?? DBNull.Value);
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }
}
