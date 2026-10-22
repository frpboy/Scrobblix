using Scrobblix.Domain.Queue;

namespace Scrobblix.Application.ReadModels;

public sealed record LovedTrackItem(
    string TrackKey,
    string SourcePlayId,
    string Artist,
    string Title,
    string? Album,
    bool LocalLoved,
    bool? ProviderLoved,
    string? CurrentActionId,
    ProviderActionType? CurrentActionType,
    ProviderActionState? CurrentActionState,
    string? LastReasonCode,
    DateTimeOffset UpdatedUtc);
