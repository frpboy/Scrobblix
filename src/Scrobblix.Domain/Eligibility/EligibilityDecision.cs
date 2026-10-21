namespace Scrobblix.Domain.Eligibility;

public sealed record EligibilityDecision(bool IsEligible, int? ThresholdMilliseconds, string? ReasonCode);
