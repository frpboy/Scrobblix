using Scrobblix.Application.Abstractions;
using Scrobblix.Domain.History;

namespace Scrobblix.Application.Ingestion;

public sealed class PlaybackObservationPollingService(
    IPlaybackObservationSource source,
    PlaybackObservationIngestionService ingestion)
{
    private static readonly TimeSpan DefaultPollInterval = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan DefaultMaximumFailureBackoff = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan InitialFailureBackoff = TimeSpan.FromSeconds(1);

    public async Task<ObservationIngestionResult?> PollOnceAsync(CancellationToken cancellationToken)
    {
        var observation = await source.GetCurrentAsync(cancellationToken);
        return observation is null
            ? null
            : await ingestion.IngestAsync(observation, cancellationToken);
    }

    public async Task RunAsync(
        Func<PlayObservation, CancellationToken, Task> onObservationAsync,
        Func<CancellationToken, Task> onSourceUnavailableAsync,
        Action<Exception, int> onRecoverableFailure,
        Action<int>? onRecovered,
        CancellationToken cancellationToken,
        TimeSpan? pollInterval = null,
        TimeSpan? maximumFailureBackoff = null)
    {
        ArgumentNullException.ThrowIfNull(onObservationAsync);
        ArgumentNullException.ThrowIfNull(onSourceUnavailableAsync);
        ArgumentNullException.ThrowIfNull(onRecoverableFailure);

        var interval = pollInterval ?? DefaultPollInterval;
        var maximumBackoff = maximumFailureBackoff ?? DefaultMaximumFailureBackoff;
        if (interval < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(pollInterval));
        }
        if (maximumBackoff < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumFailureBackoff));
        }

        var consecutiveFailures = 0;
        while (!cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(interval, cancellationToken);

            PlayObservation? observation;
            try
            {
                observation = await source.GetCurrentAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                consecutiveFailures = await HandleRecoverableFailureAsync(
                    exception,
                    consecutiveFailures,
                    onSourceUnavailableAsync,
                    onRecoverableFailure,
                    maximumBackoff,
                    cancellationToken);
                continue;
            }

            try
            {
                if (observation is null)
                {
                    await onSourceUnavailableAsync(cancellationToken);
                }
                else
                {
                    await onObservationAsync(observation, cancellationToken);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                // Processing one sample must not permanently kill the source poller.
                // Treat it as an observation gap and retry with bounded backoff.
                consecutiveFailures = await HandleRecoverableFailureAsync(
                    exception,
                    consecutiveFailures,
                    onSourceUnavailableAsync,
                    onRecoverableFailure,
                    maximumBackoff,
                    cancellationToken,
                    markSourceUnavailable: observation is not null);
                continue;
            }

            if (consecutiveFailures > 0)
            {
                var recoveredAfterFailures = consecutiveFailures;
                consecutiveFailures = 0;
                onRecovered?.Invoke(recoveredAfterFailures);
            }
        }
    }

    private static async Task<int> HandleRecoverableFailureAsync(
        Exception exception,
        int previousFailureCount,
        Func<CancellationToken, Task> onSourceUnavailableAsync,
        Action<Exception, int> onRecoverableFailure,
        TimeSpan maximumBackoff,
        CancellationToken cancellationToken,
        bool markSourceUnavailable = true)
    {
        var failureCount = previousFailureCount + 1;
        onRecoverableFailure(exception, failureCount);

        if (markSourceUnavailable)
        {
            try
            {
                await onSourceUnavailableAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception unavailableException)
            {
                // Preserve the original failure count, but make a failed gap transition
                // observable. The next poll must still be attempted.
                onRecoverableFailure(unavailableException, failureCount);
            }
        }

        await Task.Delay(CalculateFailureBackoff(failureCount, maximumBackoff), cancellationToken);
        return failureCount;
    }

    private static TimeSpan CalculateFailureBackoff(int failureCount, TimeSpan maximumBackoff)
    {
        if (maximumBackoff <= TimeSpan.Zero)
        {
            return TimeSpan.Zero;
        }

        var exponent = Math.Clamp(failureCount - 1, 0, 16);
        var requestedMilliseconds = InitialFailureBackoff.TotalMilliseconds * Math.Pow(2, exponent);
        return TimeSpan.FromMilliseconds(Math.Min(requestedMilliseconds, maximumBackoff.TotalMilliseconds));
    }
}
