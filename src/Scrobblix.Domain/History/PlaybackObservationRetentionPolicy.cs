namespace Scrobblix.Domain.History;

public static class PlaybackObservationRetentionPolicy
{
    // Retention for raw playback observations approved by the repository owner on 2026-11-06 (issue #98):
    // raw diagnostic observations are deleted after 30 days; canonical plays,
    // provider actions and ambiguous submissions are never touched by cleanup.
    public static readonly TimeSpan RetentionPeriod = TimeSpan.FromDays(30);
    public const int PruneBatchSize = 500;
}
