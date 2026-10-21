namespace Scrobblix.Application.ReadModels;

public sealed record TopArtistStatistic(
    string Artist,
    int SessionCount,
    long ConfirmedListeningMilliseconds);
