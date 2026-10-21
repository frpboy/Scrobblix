namespace Scrobblix.Domain.Diagnostics;

public static class ReasonCodes
{
    public static class Playback
    {
        public const string WaitingForEligibility = "playback.waiting_for_eligibility";
        public const string TrackTooShort = "playback.track_too_short";
        public const string DurationUnknown = "playback.duration_unknown";
        public const string InsufficientConfirmedTime = "playback.insufficient_confirmed_time";
        public const string SourceLostBeforeEligibility = "playback.source_lost_before_eligibility";
        public const string TrackingPaused = "playback.tracking_paused";
        public const string NonMusic = "playback.non_music";
        public const string Advertisement = "playback.advertisement";
    }

    public static class Metadata
    {
        public const string MissingArtist = "metadata.missing_artist";
        public const string MissingTitle = "metadata.missing_title";
        public const string InvalidDuration = "metadata.invalid_duration";
    }

    public static class Source
    {
        public const string ExcludedByUser = "source.excluded_by_user";
        public const string LowerPriorityObservation = "source.lower_priority_observation";
    }

    public static class Duplicate
    {
        public const string MergedSources = "duplicate.merged_sources";
        public const string ProviderActionExists = "duplicate.provider_action_exists";
    }

    public static class Provider
    {
        public const string LeaseExpired = "provider.lease_expired";
        public const string Accepted = "provider.accepted";
        public const string IgnoredArtist = "provider.ignored.artist";
        public const string IgnoredTrack = "provider.ignored.track";
        public const string IgnoredTimestampOld = "provider.ignored.timestamp_old";
        public const string IgnoredTimestampNew = "provider.ignored.timestamp_new";
        public const string IgnoredDailyLimit = "provider.ignored.daily_limit";
        public const string AuthRequired = "provider.auth.required";
        public const string AuthInvalidSession = "provider.auth.invalid_session";
        public const string InvalidApiKey = "provider.invalid_api_key";
        public const string InvalidSignature = "provider.invalid_signature";
        public const string SuspendedKey = "provider.suspended_key";
        public const string RateLimited = "provider.rate_limited";
        public const string TemporaryUnavailable = "provider.temporary_unavailable";
        public const string ResponseInvalid = "provider.response_invalid";
    }

    public static class Extension
    {
        public const string NotInstalled = "extension.not_installed";
        public const string NativeHostMissing = "extension.native_host_missing";
        public const string ProtocolVersionMismatch = "extension.protocol_version_mismatch";
        public const string PermissionRequired = "extension.permission_required";
    }

    public static class Gsmtc
    {
        public const string Unavailable = "gsmtc.unavailable";
    }

    public static class Storage
    {
        public const string WriteFailed = "storage.write_failed";
        public const string DatabaseIntegrityFailed = "storage.database_integrity_failed";
    }

    public static class System
    {
        public const string ClockInvalid = "system.clock_invalid";
    }
}
