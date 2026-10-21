namespace Scrobblix.Domain.Queue;

public sealed record ProviderActionLease(
    string ActionId,
    string PlayId,
    string Provider,
    string AccountId,
    string? Artist,
    string? Title,
    string? Album,
    DateTimeOffset StartedUtc,
    int? DurationMilliseconds,
    int AttemptCount,
    ProviderActionType ActionType = ProviderActionType.Scrobble);
