namespace Scrobblix.Domain.Playback;

/// <summary>Accumulates only intervals explicitly observed as playing on a monotonic clock.</summary>
public sealed class ConfirmedPlayedTime
{
    private long? _playingSinceMilliseconds;
    private long _lastObservedMilliseconds;

    public long TotalMilliseconds { get; private set; }

    public bool IsPlaying => _playingSinceMilliseconds is not null;

    public void SetPlaying(bool isPlaying, long monotonicMilliseconds)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(monotonicMilliseconds, _lastObservedMilliseconds);

        _lastObservedMilliseconds = monotonicMilliseconds;

        if (isPlaying)
        {
            _playingSinceMilliseconds ??= monotonicMilliseconds;
            return;
        }

        if (_playingSinceMilliseconds is { } playingSinceMilliseconds)
        {
            TotalMilliseconds += monotonicMilliseconds - playingSinceMilliseconds;
            _playingSinceMilliseconds = null;
        }
    }
}
