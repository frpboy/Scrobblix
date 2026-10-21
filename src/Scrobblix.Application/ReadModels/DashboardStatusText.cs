namespace Scrobblix.Application.ReadModels;

public static class DashboardStatusText
{
    public static string TodayHeadline(TodaySummary summary) => summary.DetectedSessionCount == 0
        ? "No listening sessions today"
        : $"{summary.DetectedSessionCount} listening session(s) today";

    public static string TodayDetail(TodaySummary summary) =>
        $"Confirmed listening: {FormatDuration(summary.ConfirmedListeningMilliseconds)} · Eligible: {summary.EligibleSessionCount} · Accepted: {summary.AcceptedScrobbleCount}";

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

    private static string FormatDuration(long milliseconds) =>
        TimeSpan.FromMilliseconds(milliseconds).ToString(@"h\:mm\:ss", System.Globalization.CultureInfo.InvariantCulture);
}
