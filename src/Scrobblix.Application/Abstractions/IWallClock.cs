namespace Scrobblix.Application.Abstractions;

public interface IWallClock
{
    DateTimeOffset UtcNow { get; }
}
