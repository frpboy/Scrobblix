using Scrobblix.Domain.Metadata;
using Scrobblix.Domain.Playback;

namespace Scrobblix.Domain.History;

public sealed record PlayObservation(
    SourceEventKey SourceEventKey,
    DateTimeOffset CapturedAtUtc,
    DateTimeOffset ObservedAtUtc,
    string SourceName,
    string? ApplicationId,
    string? Artist,
    string? Title,
    string? Album,
    int? DurationMilliseconds,
    long? PositionMilliseconds,
    PlayState PlaybackState,
    NormalizedMetadataValue? NormalizedArtist,
    NormalizedMetadataValue? NormalizedTitle);
