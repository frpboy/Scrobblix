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

        var canonicalIsActivelyPlaying = _canonicalObservation.PlaybackState == PlayState.Playing;
        var candidateIsActivelyPlaying = observation.PlaybackState == PlayState.Playing;
        var capturedGap = Math.Abs((observation.CapturedAtUtc - _canonicalObservation.CapturedAtUtc).TotalSeconds);
        if (canonicalIsActivelyPlaying
            && candidateIsActivelyPlaying
            && capturedGap <= 8
            && IsSameTrack(_canonicalObservation, observation)
            && Priority(observation.SourceName) < Priority(_canonicalObservation.SourceName))
        {
            return new SourceArbitrationDecision(false, ReasonCodes.Source.LowerPriorityObservation, _canonicalObservation);
        }

        _canonicalObservation = observation;
        return new SourceArbitrationDecision(true, null, observation);
    }

    private static bool IsSameTrack(PlayObservation left, PlayObservation right)
    {
        var leftTitle = Normalize(left.NormalizedTitle?.ComparisonKey ?? left.Title);
        var rightTitle = Normalize(right.NormalizedTitle?.ComparisonKey ?? right.Title);
        if (leftTitle.Length == 0 || !string.Equals(leftTitle, rightTitle, StringComparison.Ordinal))
        {
            return false;
        }

        var leftArtist = Normalize(left.NormalizedArtist?.ComparisonKey ?? left.Artist);
        var rightArtist = Normalize(right.NormalizedArtist?.ComparisonKey ?? right.Artist);
        if (leftArtist.Length > 0 && rightArtist.Length > 0
            && !string.Equals(leftArtist, rightArtist, StringComparison.Ordinal))
        {
            return false;
        }

        if (left.DurationMilliseconds is > 0 && right.DurationMilliseconds is > 0)
        {
            var difference = Math.Abs((long)left.DurationMilliseconds.Value - right.DurationMilliseconds.Value);
            var tolerance = Math.Max(8000L, Math.Max(left.DurationMilliseconds.Value, right.DurationMilliseconds.Value) * 0.04);
            if (difference > tolerance)
            {
                return false;
            }
        }

        return true;
    }

    private static string Normalize(string? value) =>
        string.Concat((value ?? string.Empty).Where(char.IsLetterOrDigit)).ToUpperInvariant();

    private static int Priority(string sourceName) => sourceName.ToUpperInvariant() switch
    {
        "GSMTC" => 2,
        "BROWSER" => 1,
        _ => 0,
    };
}
