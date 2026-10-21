namespace Scrobblix.Application.Providers;

public interface IScrobbleProvider
{
    Task<ProviderSubmissionResult> UpdateNowPlayingAsync(ScrobbleSubmission submission, CancellationToken cancellationToken);
    Task<ProviderSubmissionResult> SubmitScrobbleAsync(ScrobbleSubmission submission, CancellationToken cancellationToken);
}
