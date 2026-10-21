using Scrobblix.Domain.Eligibility;
using Scrobblix.Domain.History;

namespace Scrobblix.Domain.Playback;

public sealed class CanonicalPlayTracker
{
    private readonly ConfirmedPlayedTime _confirmedTime = new();
    private readonly CanonicalPlayState _state = new();
    private long _lastMonotonicMilliseconds;

    public CanonicalPlayState State => _state;

    public long ConfirmedPlayedMilliseconds => _confirmedTime.GetTotalMilliseconds(_lastMonotonicMilliseconds);

    public EligibilityDecision Observe(PlayObservation observation, long monotonicMilliseconds)
    {
        _lastMonotonicMilliseconds = monotonicMilliseconds;
        if (_state.Current == PlayState.Candidate && observation.PlaybackState == PlayState.Playing)
        {
            _state.TransitionTo(PlayState.Playing);
        }
        else if (_state.Current == PlayState.Paused && observation.PlaybackState == PlayState.Playing)
        {
            _state.TransitionTo(PlayState.Playing);
        }

        _confirmedTime.SetPlaying(observation.PlaybackState == PlayState.Playing, monotonicMilliseconds);
        var decision = ScrobbleEligibility.Evaluate(
            observation.Artist,
            observation.Title,
            observation.DurationMilliseconds,
            _confirmedTime.GetTotalMilliseconds(monotonicMilliseconds));
        if (decision.IsEligible && !_state.IsEligible)
        {
            _state.MarkEligible();
        }

        if (_state.Current == PlayState.Playing && observation.PlaybackState == PlayState.Paused)
        {
            _state.TransitionTo(PlayState.Paused);
        }

        return decision;
    }
}
