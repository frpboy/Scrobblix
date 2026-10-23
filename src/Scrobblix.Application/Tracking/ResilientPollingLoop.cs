namespace Scrobblix.Application.Tracking;

public static class ResilientPollingLoop
{
    public static TimeSpan GetRetryDelay(int failureStreak)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(failureStreak, 1);
        var exponent = Math.Clamp(failureStreak - 1, 0, 5);
        return TimeSpan.FromSeconds(Math.Min(30, 1 << exponent));
    }

    public static async Task RunAsync(
        Func<CancellationToken, Task> pollOnceAsync,
        TimeSpan pollInterval,
        Func<int, TimeSpan> retryDelay,
        Action<Exception, int> onFailure,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pollOnceAsync);
        ArgumentNullException.ThrowIfNull(retryDelay);
        ArgumentNullException.ThrowIfNull(onFailure);
        ArgumentOutOfRangeException.ThrowIfLessThan(pollInterval, TimeSpan.Zero);
        var failures = 0;
        while (!cancellationToken.IsCancellationRequested)
        {
            var delay = failures == 0 ? pollInterval : retryDelay(failures);
            ArgumentOutOfRangeException.ThrowIfLessThan(delay, TimeSpan.Zero);
            try
            {
                await Task.Delay(delay, cancellationToken);
                await pollOnceAsync(cancellationToken);
                failures = 0;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                failures = Math.Min(failures + 1, 6);
                onFailure(exception, failures);
            }
        }
    }

    public static Task RunAsync(
        Func<CancellationToken, Task> pollOnceAsync,
        TimeSpan pollInterval,
        Action<Exception, int> onFailure,
        CancellationToken cancellationToken) =>
        RunAsync(pollOnceAsync, pollInterval, GetRetryDelay, onFailure, cancellationToken);
}
