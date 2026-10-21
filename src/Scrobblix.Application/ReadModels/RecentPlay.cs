namespace Scrobblix.Application.ReadModels;

public sealed record RecentPlay(
    string PlayId,
    DateTimeOffset StartedUtc,
    string? Artist,
    string? Title,
    string? Album,
    int? DurationMilliseconds,
    long ConfirmedPlayedMilliseconds,
    DateTimeOffset? EligibleUtc,
    string? TerminalReasonCode,
    string? ArtworkPath = null);
