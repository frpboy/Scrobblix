using Scrobblix.Domain.Queue;

namespace Scrobblix.Application.ReadModels;

public sealed record LovedTrackSummary(
    string PlayId,
    string? Artist,
    string? Title,
    string? Album,
    string? ArtworkPath,
    ProviderActionType RequestedAction,
    ProviderActionState State,
    int AttemptCount,
    string? LastReasonCode,
    DateTimeOffset UpdatedUtc);
