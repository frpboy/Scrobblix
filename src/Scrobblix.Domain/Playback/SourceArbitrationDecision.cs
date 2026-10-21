using Scrobblix.Domain.History;

namespace Scrobblix.Domain.Playback;

public sealed record SourceArbitrationDecision(
    bool IsAccepted,
    string? ReasonCode,
    PlayObservation CanonicalObservation);
