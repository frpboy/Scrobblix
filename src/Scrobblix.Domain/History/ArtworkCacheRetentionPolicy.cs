namespace Scrobblix.Domain.History;

public static class ArtworkCacheRetentionPolicy
{
    // Cached artwork retention approved by the repository owner on 2026-11-07 (issue #122):
    // cached artwork files older than 30 days are deleted, except files still referenced by
    // retained listening history (plays.artwork_path), which are preserved. Listening history
    // itself is kept and is not covered by this policy; raw playback diagnostics keep their
    // separate 30-day policy (PlaybackObservationRetentionPolicy).
    public static readonly TimeSpan RetentionPeriod = TimeSpan.FromDays(30);
    public const int PruneBatchSize = 200;
    // Crash-interrupted artwork writes leave orphaned temporary files; they are safe to remove
    // once no in-flight writer can still own them.
    public static readonly TimeSpan TemporaryFileGracePeriod = TimeSpan.FromDays(1);
}
