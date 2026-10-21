using Scrobblix.Domain.Diagnostics;

namespace Scrobblix.Domain.Eligibility;

public static class ScrobbleEligibility
{
    private const int MinimumDurationMilliseconds = 30_000;
    private const int MaximumThresholdMilliseconds = 240_000;

    public static EligibilityDecision Evaluate(
        string? artist,
        string? title,
        int? durationMilliseconds,
        long confirmedPlayedMilliseconds,
        bool isExcluded = false,
        bool isDuplicate = false,
        bool isNonMusic = false,
        bool isAdvertisement = false,
        bool hasProviderAction = false)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(confirmedPlayedMilliseconds);

        if (string.IsNullOrWhiteSpace(artist))
        {
            return Ineligible(ReasonCodes.Metadata.MissingArtist);
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            return Ineligible(ReasonCodes.Metadata.MissingTitle);
        }

        if (durationMilliseconds is null)
        {
            return Ineligible(ReasonCodes.Playback.DurationUnknown);
        }

        if (durationMilliseconds <= 0)
        {
            return Ineligible(ReasonCodes.Metadata.InvalidDuration);
        }

        if (durationMilliseconds <= MinimumDurationMilliseconds)
        {
            return Ineligible(ReasonCodes.Playback.TrackTooShort);
        }

        if (isExcluded)
        {
            return Ineligible(ReasonCodes.Source.ExcludedByUser);
        }

        if (isDuplicate)
        {
            return Ineligible(ReasonCodes.Duplicate.MergedSources);
        }

        if (hasProviderAction)
        {
            return Ineligible(ReasonCodes.Duplicate.ProviderActionExists);
        }

        if (isNonMusic)
        {
            return Ineligible(ReasonCodes.Playback.NonMusic);
        }

        if (isAdvertisement)
        {
            return Ineligible(ReasonCodes.Playback.Advertisement);
        }

        var thresholdMilliseconds = Math.Min(durationMilliseconds.Value / 2, MaximumThresholdMilliseconds);
        return confirmedPlayedMilliseconds >= thresholdMilliseconds
            ? new EligibilityDecision(true, thresholdMilliseconds, null)
            : new EligibilityDecision(false, thresholdMilliseconds, ReasonCodes.Playback.InsufficientConfirmedTime);
    }

    private static EligibilityDecision Ineligible(string reasonCode) => new(false, null, reasonCode);
}
