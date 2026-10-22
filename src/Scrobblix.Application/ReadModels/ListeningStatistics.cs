namespace Scrobblix.Application.ReadModels;

public sealed record ArtistListeningSummary(string Artist, int PlayCount, long ConfirmedListeningMilliseconds);

public sealed record ListeningStatistics(
    int TotalSessionCount,
    int EligibleSessionCount,
    int AcceptedScrobbleCount,
    long ConfirmedListeningMilliseconds,
    IReadOnlyList<ArtistListeningSummary> TopArtists);
