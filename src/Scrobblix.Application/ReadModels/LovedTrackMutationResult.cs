namespace Scrobblix.Application.ReadModels;

public sealed record LovedTrackMutationResult(
    string TrackKey,
    bool IsLoved,
    bool Changed,
    bool ActionQueued,
    string? Reason);
