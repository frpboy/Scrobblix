using Scrobblix.Domain.Eligibility;

namespace Scrobblix.Application.Ingestion;

public sealed record PlaybackTrackingStatus(
    EligibilityDecision Decision,
    long ConfirmedPlayedMilliseconds,
    bool DidPersistEligiblePlay,
    bool DidStartPlay = false,
    string? PlayId = null);
