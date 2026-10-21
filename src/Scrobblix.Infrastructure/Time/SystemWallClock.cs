using Scrobblix.Application.Abstractions;

namespace Scrobblix.Infrastructure.Time;

public sealed class SystemWallClock : IWallClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
