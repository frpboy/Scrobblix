namespace Scrobblix.Application.Providers;

public sealed record ScrobbleSubmission(
    string Artist,
    string Track,
    DateTimeOffset StartedUtc,
    string SessionKey,
    string? Album = null,
    int? DurationSeconds = null);
