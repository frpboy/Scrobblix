namespace Scrobblix.Application.ReadModels;

public sealed record QueueSummary(
    int PendingCount,
    int InFlightCount,
    int AcceptedCount,
    int RetryScheduledCount,
    int AuthBlockedCount,
    int PermanentFailureCount);
