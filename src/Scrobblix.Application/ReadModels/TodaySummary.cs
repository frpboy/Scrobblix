namespace Scrobblix.Application.ReadModels;

/// <summary>
/// Truthful local-day totals projected from SQLite, independent of Last.fm availability.
/// </summary>
public sealed record TodaySummary(
    int ListeningSessionCount,
    int EligiblePlayCount,
    int AcceptedScrobbleCount,
    long ConfirmedListeningMilliseconds);
