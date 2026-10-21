using Scrobblix.Domain.History;

namespace Scrobblix.Application.Ingestion;

public static class PlaybackObservationValidator
{
    private const int MaximumTextLength = 1_024;

    public static void Validate(PlayObservation observation)
    {
        ArgumentNullException.ThrowIfNull(observation);
        ArgumentException.ThrowIfNullOrWhiteSpace(observation.SourceEventKey.SourceInstanceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(observation.SourceName);
        ArgumentOutOfRangeException.ThrowIfNegative(observation.SourceEventKey.SequenceNumber);
        if (observation.ObservedAtUtc > observation.CapturedAtUtc.AddMinutes(5))
        {
            throw new ArgumentException("Observed time cannot be materially later than capture time.", nameof(observation));
        }

        ValidateText(observation.ApplicationId, nameof(observation.ApplicationId));
        ValidateText(observation.Artist, nameof(observation.Artist));
        ValidateText(observation.Title, nameof(observation.Title));
        ValidateText(observation.Album, nameof(observation.Album));
        if (observation.DurationMilliseconds is <= 0 || observation.PositionMilliseconds is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(observation));
        }
    }

    private static void ValidateText(string? value, string name)
    {
        if (value?.Length > MaximumTextLength || value?.Any(char.IsControl) == true)
        {
            throw new ArgumentException("Input text is invalid or exceeds the supported limit.", name);
        }
    }
}
