using Scrobblix.Domain.Eligibility;
using Scrobblix.Domain.Queue;

namespace Scrobblix.Domain.History;

public sealed record LocalPlayRecord(
    string PlayId,
    DateTimeOffset FirstObservedUtc,
    DateTimeOffset LastObservedUtc,
    string? Artist,
    string? Title,
    string? Album,
    int? DurationMilliseconds,
    long ConfirmedPlayedMilliseconds,
    EligibilityDecision Eligibility,
    ProviderActionState? QueueState,
    string? QueueReasonCode,
    string? LocalReasonCode,
    string? ArtworkPath = null,
    string? AlbumArtist = null,
    int? TrackNumber = null,
    int? ReleaseYear = null,
    string? RecordingMusicBrainzId = null,
    string? ReleaseMusicBrainzId = null,
    string? ArtworkSource = null);
