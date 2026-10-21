using Scrobblix.Domain.Diagnostics;
using Scrobblix.Domain.History;

namespace Scrobblix.Domain.Playback;

public sealed class PlaybackSourceArbitrator
{
    private PlayObservation? _canonicalObservation;

    public SourceArbitrationDecision Consider(PlayObservation observation)
    {
        if (_canonicalObservation is null)
        {
            _canonicalObservation = observation;
            return new SourceArbitrationDecision(true, null, observation);
        }

        if (IsSameTrack(_canonicalObservation, observation)
            && Priority(observation.SourceName) < Priority(_canonicalObservation.SourceName))
        {
            return new SourceArbitrationDecision(false, ReasonCodes.Source.LowerPriorityObservation, _canonicalObservation);
        }

        _canonicalObservation = observation;
        return new SourceArbitrationDecision(true, null, observation);
    }

    private static bool IsSameTrack(PlayObservation left, PlayObservation right) =>
        string.Equals(left.NormalizedArtist?.ComparisonKey, right.NormalizedArtist?.ComparisonKey, StringComparison.Ordinal)
        && string.Equals(left.NormalizedTitle?.ComparisonKey, right.NormalizedTitle?.ComparisonKey, StringComparison.Ordinal)
        && left.DurationMilliseconds == right.DurationMilliseconds;

    private static int Priority(string sourceName) => sourceName.ToUpperInvariant() switch
    {
        "GSMTC" => 2,
        "BROWSER" => 1,
        _ => 0,
    };
}
