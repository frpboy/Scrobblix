namespace Scrobblix.Application.Abstractions;

public interface IMonotonicClock
{
    long Timestamp { get; }

    TimeSpan Elapsed(long startTimestamp, long endTimestamp);
}
