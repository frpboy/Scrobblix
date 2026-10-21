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
        if (awaitingDelivery == 0 && summary.SubmissionOutcomeUnknownCount == 0)
            return "No queued actions";
        if (awaitingDelivery == 0)
            return $"{summary.SubmissionOutcomeUnknownCount} action(s) need outcome review";
        return summary.SubmissionOutcomeUnknownCount == 0
            ? $"{awaitingDelivery} action(s) awaiting delivery"
            : $"{awaitingDelivery} action(s) awaiting delivery; {summary.SubmissionOutcomeUnknownCount} need outcome review";
    }

    public static string QueueDetail(QueueSummary summary)
    {
        var detail = summary.AuthBlockedCount + summary.PermanentFailureCount == 0
            ? $"Accepted: {summary.AcceptedCount}"
            : $"Blocked: {summary.AuthBlockedCount}; failed: {summary.PermanentFailureCount}";
        return summary.SubmissionOutcomeUnknownCount == 0
            ? detail
            : $"{detail}; outcome unknown: {summary.SubmissionOutcomeUnknownCount}";
    }

    private static string FormatDuration(long milliseconds) =>
        TimeSpan.FromMilliseconds(milliseconds).ToString(@"h\:mm\:ss", System.Globalization.CultureInfo.InvariantCulture);
}
