namespace Philiprehberger.RateLimiter;

/// <summary>
/// A rate limiter that uses the token bucket algorithm. Tokens are added at a fixed
/// rate up to a maximum capacity. Each acquisition consumes one token.
/// </summary>
public sealed class TokenBucketLimiter : IRateLimiter
{
    private readonly int _capacity;
    private readonly int _refillRate;
    private readonly TimeSpan _refillInterval;
    private readonly object _lock = new();
    private readonly Dictionary<string, BucketState> _buckets = new();
    private readonly BucketState _defaultBucket;

    /// <summary>
    /// Creates a new <see cref="TokenBucketLimiter"/> instance.
    /// </summary>
    /// <param name="capacity">The maximum number of tokens the bucket can hold.</param>
    /// <param name="refillRate">The number of tokens added per refill interval.</param>
    /// <param name="refillInterval">The time between token refills.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="capacity"/> or <paramref name="refillRate"/> is less than 1,
    /// or when <paramref name="refillInterval"/> is not positive.
    /// </exception>
    public TokenBucketLimiter(int capacity, int refillRate, TimeSpan refillInterval)
    {
        if (capacity < 1)
            throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Capacity must be at least 1.");
        if (refillRate < 1)
            throw new ArgumentOutOfRangeException(nameof(refillRate), refillRate, "Refill rate must be at least 1.");
        if (refillInterval <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(refillInterval), refillInterval, "Refill interval must be positive.");

        _capacity = capacity;
        _refillRate = refillRate;
        _refillInterval = refillInterval;
        _defaultBucket = new BucketState(capacity);
    }

    /// <inheritdoc />
    public int Available
    {
        get
        {
            lock (_lock)
            {
                Refill(_defaultBucket);
                return _defaultBucket.Tokens;
            }
        }
    }

    /// <inheritdoc />
    public int Limit => _capacity;

    /// <inheritdoc />
    public bool TryAcquire(string? key = null)
    {
        lock (_lock)
        {
            var bucket = GetBucket(key);
            Refill(bucket);

            if (bucket.Tokens <= 0)
                return false;

            bucket.Tokens--;
            return true;
        }
    }

    /// <inheritdoc />
    public async Task WaitAsync(string? key = null, CancellationToken ct = default)
    {
        while (!ct.IsCancellationRequested)
        {
            if (TryAcquire(key))
                return;

            await Task.Delay(TimeSpan.FromMilliseconds(10), ct).ConfigureAwait(false);
        }

        ct.ThrowIfCancellationRequested();
    }

    private BucketState GetBucket(string? key)
    {
        if (key is null)
            return _defaultBucket;

        if (!_buckets.TryGetValue(key, out var bucket))
        {
            bucket = new BucketState(_capacity);
            _buckets[key] = bucket;
        }

        return bucket;
    }

    private void Refill(BucketState bucket)
    {
        var now = DateTime.UtcNow;
        var elapsed = now - bucket.LastRefill;
        var intervals = (int)(elapsed.Ticks / _refillInterval.Ticks);

        if (intervals <= 0)
            return;

        bucket.Tokens = Math.Min(_capacity, bucket.Tokens + intervals * _refillRate);
        bucket.LastRefill = bucket.LastRefill.Add(TimeSpan.FromTicks(intervals * _refillInterval.Ticks));
    }

    private sealed class BucketState
    {
        public int Tokens;
        public DateTime LastRefill;

        public BucketState(int tokens)
        {
            Tokens = tokens;
            LastRefill = DateTime.UtcNow;
        }
    }
}
