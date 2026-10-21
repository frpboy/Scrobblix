namespace Scrobblix.Domain.Playback;

public sealed class CanonicalPlayState
{
    public PlayState Current { get; private set; } = PlayState.Candidate;

    public bool IsEligible { get; private set; }

    public string? TerminalReasonCode { get; private set; }

    public void TransitionTo(PlayState next, string? terminalReasonCode = null)
    {
        if (next == Current)
        {
            return;
        }

        if (!IsAllowed(Current, next))
        {
            throw new InvalidOperationException($"Cannot transition from {Current} to {next}.");
        }

        if (IsTerminal(next) && next != PlayState.Ended && string.IsNullOrWhiteSpace(terminalReasonCode))
        {
            throw new ArgumentException("A terminal transition requires a reason code.", nameof(terminalReasonCode));
        }

        Current = next;
        TerminalReasonCode = next == PlayState.Ended ? null : terminalReasonCode;
    }

    public void MarkEligible()
    {
        if (Current != PlayState.Playing)
        {
            throw new InvalidOperationException("Only an active play can become eligible.");
        }

        IsEligible = true;
    }

    private static bool IsAllowed(PlayState current, PlayState next) => (current, next) switch
    {
        (PlayState.Candidate, PlayState.Playing or PlayState.Ignored or PlayState.Duplicate) => true,
        (PlayState.Playing, PlayState.Paused or PlayState.Unknown or PlayState.Ended or PlayState.Duplicate) => true,
        (PlayState.Paused, PlayState.Playing or PlayState.Abandoned) => true,
        (PlayState.Unknown, PlayState.Playing or PlayState.Abandoned) => true,
        _ => false,
    };

    private static bool IsTerminal(PlayState state) => state is PlayState.Ended or PlayState.Abandoned or PlayState.Ignored or PlayState.Duplicate;
}
