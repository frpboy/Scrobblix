using Scrobblix.Domain.Queue;

namespace Scrobblix.Application.ReadModels;

public sealed record RecentQueueAction(
    string ActionId,
    string? PlayId,
    string Provider,
    string AccountId,
    ProviderActionType ActionType,
    ProviderActionState State,
    int AttemptCount,
    DateTimeOffset DueUtc,
    DateTimeOffset UpdatedUtc,
    string? LastReasonCode,
    string? Artist,
    string? Title,
    string? Album);
