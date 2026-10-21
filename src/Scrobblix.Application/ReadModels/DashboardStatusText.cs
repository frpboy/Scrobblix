namespace Scrobblix.Application.ReadModels;

public static class DashboardStatusText
{
    public static string QueueHeadline(QueueSummary summary)
    {
        var awaitingDelivery = summary.PendingCount + summary.RetryScheduledCount + summary.InFlightCount;
        return awaitingDelivery == 0
            ? "No queued actions"
            : $"{awaitingDelivery} action(s) awaiting delivery";
    }

    public static string QueueDetail(QueueSummary summary) =>
        summary.AuthBlockedCount + summary.PermanentFailureCount == 0
            ? $"Accepted: {summary.AcceptedCount}"
            : $"Blocked: {summary.AuthBlockedCount}; failed: {summary.PermanentFailureCount}";
}
