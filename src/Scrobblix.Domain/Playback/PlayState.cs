namespace Scrobblix.Domain.Playback;

public enum PlayState
{
    Candidate,
    Playing,
    Paused,
    Unknown,
    Ended,
    Abandoned,
    Ignored,
    Duplicate,
}
