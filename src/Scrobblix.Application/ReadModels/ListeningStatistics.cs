namespace Scrobblix.Application.ReadModels;

public sealed record ListeningStatistics(
    int TotalSessions,
    int EligiblePlays,
    int UniqueArtists,
    long ConfirmedListeningMilliseconds,
    int SessionsLast30Days,
    IReadOnlyList<TopArtistStatistic> TopArtists);
