using Scrobblix.Domain.Playback;

namespace Scrobblix.Application.Ingestion;

/// <summary>
/// Maps the browser companion protocol's state strings into the domain playback states.
/// An ended media element is not a pause; keeping it distinct lets the tracker close/replay
/// sessions accurately and prevents an ended browser track from appearing paused indefinitely.
/// </summary>
public static class BrowserPlaybackStateMapper
{
    public static PlayState Parse(string? state) => state switch
    {
        "playing" => PlayState.Playing,
        "paused" => PlayState.Paused,
        "ended" => PlayState.Ended,
        _ => PlayState.Unknown,
    };
}
