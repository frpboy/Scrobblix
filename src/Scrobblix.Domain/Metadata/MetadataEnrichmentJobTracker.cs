namespace Scrobblix.Domain.Metadata;

/// <summary>
/// Tracks metadata enrichment job keys with explicit bounds: a limited number of
/// pending or running jobs, and a bounded FIFO window of completed job keys that
/// suppresses duplicate enrichment. Failed or cancelled jobs are abandoned without
/// suppression so a later poll can retry them; completed keys evict oldest-first
/// once the window is full, after which a still-active play may be enriched again.
/// </summary>
public sealed class MetadataEnrichmentJobTracker
{
    private readonly int _maxPendingJobs;
    private readonly int _completedCapacity;
    private readonly HashSet<string> _pendingJobs = new(StringComparer.Ordinal);
    private readonly HashSet<string> _completedJobs = new(StringComparer.Ordinal);
    private readonly Queue<string> _completedOrder = new();
    private readonly object _sync = new();

    public MetadataEnrichmentJobTracker(int maxPendingJobs, int completedCapacity)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxPendingJobs, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(completedCapacity, 1);
        _maxPendingJobs = maxPendingJobs;
        _completedCapacity = completedCapacity;
    }

    public int PendingCount
    {
        get
        {
            lock (_sync)
            {
                return _pendingJobs.Count;
            }
        }
    }

    public int CompletedCount
    {
        get
        {
            lock (_sync)
            {
                return _completedJobs.Count;
            }
        }
    }

    /// <summary>
    /// Reserves a pending slot for <paramref name="jobKey"/>. Returns false when the
    /// key is already pending, is still suppressed by a completed run, or the pending
    /// bound is reached; in every false case the caller skips scheduling and a later
    /// poll may schedule the job again.
    /// </summary>
    public bool TrySchedule(string jobKey)
    {
        ArgumentException.ThrowIfNullOrEmpty(jobKey);
        lock (_sync)
        {
            if (_pendingJobs.Contains(jobKey) || _completedJobs.Contains(jobKey))
            {
                return false;
            }

            if (_pendingJobs.Count >= _maxPendingJobs)
            {
                return false;
            }

            _pendingJobs.Add(jobKey);
            return true;
        }
    }

    /// <summary>
    /// Moves a pending job into the bounded completed window, evicting the oldest
    /// completed key when the window exceeds its capacity. Unknown keys are ignored.
    /// </summary>
    public void Complete(string jobKey)
    {
        ArgumentException.ThrowIfNullOrEmpty(jobKey);
        lock (_sync)
        {
            if (!_pendingJobs.Remove(jobKey))
            {
                return;
            }

            if (_completedJobs.Add(jobKey))
            {
                _completedOrder.Enqueue(jobKey);
                while (_completedJobs.Count > _completedCapacity)
                {
                    var oldestKey = _completedOrder.Dequeue();
                    _completedJobs.Remove(oldestKey);
                }
            }
        }
    }

    /// <summary>
    /// Drops a pending job without adding suppression so it can be retried by a later
    /// poll. Used for failed or cancelled enrichment work.
    /// </summary>
    public void Abandon(string jobKey)
    {
        ArgumentException.ThrowIfNullOrEmpty(jobKey);
        lock (_sync)
        {
            _pendingJobs.Remove(jobKey);
        }
    }
}
