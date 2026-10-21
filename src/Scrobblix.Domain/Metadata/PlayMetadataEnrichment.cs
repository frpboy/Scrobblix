namespace Scrobblix.Domain.Metadata;

public sealed record PlayMetadataEnrichment(
    string? AlbumArtist = null,
    int? TrackNumber = null,
    int? ReleaseYear = null,
    string? RecordingMusicBrainzId = null,
    string? ReleaseMusicBrainzId = null,
    string? ArtworkPath = null,
    string? ArtworkSource = null,
    string? Album = null);
