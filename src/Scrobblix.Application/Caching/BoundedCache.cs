namespace Scrobblix.Application.Caching;

/// <summary>
/// A small, thread-safe FIFO cache for process-local metadata results.
/// Updating an existing key does not change its eviction position.
/// </summary>
public sealed class BoundedCache<TKey, TValue> where TKey : notnull
{
    private readonly int _capacity;
    private readonly Dictionary<TKey, TValue> _entries;
    private readonly Queue<TKey> _insertionOrder = new();
    private readonly object _sync = new();

    public BoundedCache(int capacity, IEqualityComparer<TKey>? comparer = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        _capacity = capacity;
        _entries = new Dictionary<TKey, TValue>(comparer);
    }

    public int Count
    {
        get
        {
            lock (_sync)
            {
                return _entries.Count;
            }
        }
    }

    public bool TryGetValue(TKey key, out TValue value)
    {
        lock (_sync)
        {
            return _entries.TryGetValue(key, out value!);
        }
    }

    public void Set(TKey key, TValue value)
    {
        lock (_sync)
        {
            if (!_entries.ContainsKey(key))
            {
                _insertionOrder.Enqueue(key);
            }

            _entries[key] = value;
            while (_entries.Count > _capacity)
            {
                var oldestKey = _insertionOrder.Dequeue();
                _entries.Remove(oldestKey);
            }
        }
    }

    public void Clear()
    {
        lock (_sync)
        {
            _entries.Clear();
            _insertionOrder.Clear();
        }
    }
}
