using Scrobblix.Domain.Diagnostics;
using Scrobblix.Domain.Queue;

namespace Scrobblix.Application.ReadModels;

/// <summary>Local-only queue copy. Rendering never verifies or changes provider state.</summary>
public static class QueueActionPresenter
{
    public const string UnknownOutcomeHelp = "The submission result could not be confirmed. Last.fm may already have accepted it. Compare this play's start time and track on your Last.fm profile before choosing a recovery action; retrying can create a duplicate.";
    public const string ConfirmAcceptedHelp = "Only confirm after finding this exact play on your Last.fm profile, matching its start time, artist and track. An earlier play of the same track is not proof. This records your confirmation locally, without checking Last.fm, and stops delivery attempts for this action.";

    public static string StateLabel(QueueActionSummary action) => action.State switch
    {
        ProviderActionState.Accepted when action.LastReasonCode == ReasonCodes.Provider.UserConfirmedAccepted => "Accepted locally: user confirmed",
        ProviderActionState.Accepted when action.LastReasonCode == ReasonCodes.Provider.Accepted => "Accepted by provider",
        ProviderActionState.Accepted => "Accepted: evidence not specified",
        ProviderActionState.RetryScheduled => "Retry scheduled",
        ProviderActionState.RetryExhausted => "Automatic retries exhausted",
        ProviderActionState.SubmissionOutcomeUnknown => "Outcome unknown: review required",
        ProviderActionState.AuthBlocked => "Authentication blocked",
        ProviderActionState.PermanentFailure => "Failed",
        ProviderActionState.ProviderIgnored => "Ignored by provider",
        ProviderActionState.InFlight => "In progress",
        _ => action.State.ToString(),
    };

    public static string PlayStartLabel(QueueActionSummary action) => action.StartedUtc is { } started
        ? $"Play started (local): {started.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss zzz", System.Globalization.CultureInfo.InvariantCulture)}"
        : "Play start unavailable: do not match this action using its queue due time.";

    public static string QueueDueLabel(QueueActionSummary action) =>
        $"Queue due (local, not play start): {action.DueUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss zzz", System.Globalization.CultureInfo.InvariantCulture)}";
}
