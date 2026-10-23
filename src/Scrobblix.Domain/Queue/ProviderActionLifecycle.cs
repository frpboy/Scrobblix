namespace Scrobblix.Domain.Queue;

public sealed class ProviderActionLifecycle
{
    public ProviderActionState Current { get; private set; } = ProviderActionState.Pending;

    public int AttemptCount { get; private set; }

    public string? LeaseOwner { get; private set; }

    public DateTimeOffset? LeaseExpiresUtc { get; private set; }

    public string? ReasonCode { get; private set; }

    public void AcquireLease(string owner, DateTimeOffset acquiredUtc, DateTimeOffset expiresUtc)
    {
        if (Current is not ProviderActionState.Pending and not ProviderActionState.RetryScheduled)
        {
            throw new InvalidOperationException("Only due pending or retry-scheduled actions can be leased.");
        }

        if (string.IsNullOrWhiteSpace(owner) || expiresUtc <= acquiredUtc)
        {
            throw new ArgumentException("A lease requires an owner and a future expiry.");
        }

        Current = ProviderActionState.InFlight;
        AttemptCount++;
        LeaseOwner = owner;
        LeaseExpiresUtc = expiresUtc;
        ReasonCode = null;
    }

    public void Complete(ProviderActionState outcome, string reasonCode)
    {
        if (Current != ProviderActionState.InFlight || !IsCompletionOutcome(outcome) || string.IsNullOrWhiteSpace(reasonCode))
        {
            throw new InvalidOperationException("Only an in-flight action can receive a classified completion outcome.");
        }

        Current = outcome;
        ReasonCode = reasonCode;
        ClearLease();
    }

    public void UnblockAuthentication()
    {
        if (Current != ProviderActionState.AuthBlocked)
        {
            throw new InvalidOperationException("Only authentication-blocked actions can be unblocked.");
        }

        Current = ProviderActionState.Pending;
        ReasonCode = null;
    }

    public void Cancel()
    {
        if (Current is not ProviderActionState.Pending and not ProviderActionState.RetryScheduled)
        {
            throw new InvalidOperationException("Only pending or retry-scheduled actions can be cancelled.");
        }

        Current = ProviderActionState.Cancelled;
        ClearLease();
    }

    private void ClearLease()
    {
        LeaseOwner = null;
        LeaseExpiresUtc = null;
    }

    private static bool IsCompletionOutcome(ProviderActionState state) => state is ProviderActionState.Accepted
        or ProviderActionState.ProviderIgnored
        or ProviderActionState.RetryScheduled
        or ProviderActionState.AuthBlocked
        or ProviderActionState.PermanentFailure
        or ProviderActionState.SubmissionOutcomeUnknown
        or ProviderActionState.RetryExhausted;
}
