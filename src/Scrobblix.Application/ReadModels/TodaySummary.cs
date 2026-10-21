namespace Scrobblix.Application.ReadModels;

public sealed record TodaySummary(
    int DetectedSessionCount,
    int EligibleSessionCount,
    int AcceptedScrobbleCount,
    long ConfirmedListeningMilliseconds);
