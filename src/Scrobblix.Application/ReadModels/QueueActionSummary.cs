using Scrobblix.Domain.Queue;

namespace Scrobblix.Application.ReadModels;

public sealed record QueueActionSummary(
    string ActionId,
    string? PlayId,
    string Provider,
    ProviderActionType ActionType,
    ProviderActionState State,
    int AttemptCount,
    DateTimeOffset DueUtc,
    string? LastReasonCode,
    string? Artist,
    string? Title);
