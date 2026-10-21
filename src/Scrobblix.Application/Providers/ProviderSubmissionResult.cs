namespace Scrobblix.Application.Providers;

public sealed record ProviderSubmissionResult(bool Accepted, string ReasonCode, bool IsTransient);
