namespace Scrobblix.Domain.Queue;

public enum ProviderActionState
{
    Pending,
    InFlight,
    Accepted,
    ProviderIgnored,
    RetryScheduled,
    AuthBlocked,
    PermanentFailure,
    Cancelled,
}
