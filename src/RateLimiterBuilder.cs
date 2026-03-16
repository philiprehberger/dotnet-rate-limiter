namespace Philiprehberger.RateLimiter;

/// <summary>
/// A fluent builder for creating <see cref="IRateLimiter"/> instances.
/// </summary>
public sealed class RateLimiterBuilder
{
    private enum Algorithm { None, TokenBucket, FixedWindow, SlidingWindow }

    private Algorithm _algorithm = Algorithm.None;
    private int _capacity;
    private int _refillRate;
    private TimeSpan _refillInterval;
    private int _limit;
    private TimeSpan _window;
    private int _segments = 10;
    private bool _perKey;

    private RateLimiterBuilder() { }

    /// <summary>
    /// Creates a new <see cref="RateLimiterBuilder"/> instance.
    /// </summary>
    /// <returns>A new builder.</returns>
    public static RateLimiterBuilder Create() => new();

    /// <summary>
    /// Configures the builder to create a token bucket rate limiter.
    /// </summary>
    /// <param name="capacity">The maximum number of tokens.</param>
    /// <param name="refillRate">The number of tokens added per interval.</param>
    /// <param name="per">The refill interval.</param>
    /// <returns>This builder for chaining.</returns>
    public RateLimiterBuilder TokenBucket(int capacity, int refillRate, TimeSpan per)
    {
        _algorithm = Algorithm.TokenBucket;
        _capacity = capacity;
        _refillRate = refillRate;
        _refillInterval = per;
        return this;
    }

    /// <summary>
    /// Configures the builder to create a fixed window rate limiter.
    /// </summary>
    /// <param name="limit">The maximum number of permits per window.</param>
    /// <param name="window">The window duration.</param>
    /// <returns>This builder for chaining.</returns>
    public RateLimiterBuilder FixedWindow(int limit, TimeSpan window)
    {
        _algorithm = Algorithm.FixedWindow;
        _limit = limit;
        _window = window;
        return this;
    }

    /// <summary>
    /// Configures the builder to create a sliding window rate limiter.
    /// </summary>
    /// <param name="limit">The maximum number of permits per window.</param>
    /// <param name="window">The window duration.</param>
    /// <param name="segments">The number of segments. Defaults to 10.</param>
    /// <returns>This builder for chaining.</returns>
    public RateLimiterBuilder SlidingWindow(int limit, TimeSpan window, int segments = 10)
    {
        _algorithm = Algorithm.SlidingWindow;
        _limit = limit;
        _window = window;
        _segments = segments;
        return this;
    }

    /// <summary>
    /// Enables per-key partitioning. When enabled, each unique key passed to
    /// <see cref="IRateLimiter.TryAcquire"/> and <see cref="IRateLimiter.WaitAsync"/>
    /// gets its own independent rate limit.
    /// </summary>
    /// <returns>This builder for chaining.</returns>
    public RateLimiterBuilder PerKey()
    {
        _perKey = true;
        return this;
    }

    /// <summary>
    /// Builds the configured <see cref="IRateLimiter"/> instance.
    /// </summary>
    /// <returns>A new rate limiter.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no algorithm has been configured.</exception>
    public IRateLimiter Build()
    {
        IRateLimiter limiter = _algorithm switch
        {
            Algorithm.TokenBucket => new TokenBucketLimiter(_capacity, _refillRate, _refillInterval),
            Algorithm.FixedWindow => new FixedWindowLimiter(_limit, _window),
            Algorithm.SlidingWindow => new SlidingWindowLimiter(_limit, _window, _segments),
            _ => throw new InvalidOperationException("No algorithm configured. Call TokenBucket(), FixedWindow(), or SlidingWindow() before Build().")
        };

        if (_perKey)
            return new PartitionedLimiter(limiter);

        return limiter;
    }
}

/// <summary>
/// A decorator that enables per-key partitioned rate limiting by delegating
/// to a factory that creates independent limiter instances per key.
/// </summary>
internal sealed class PartitionedLimiter : IRateLimiter
{
    private readonly IRateLimiter _inner;

    public PartitionedLimiter(IRateLimiter inner)
    {
        _inner = inner;
    }

    /// <inheritdoc />
    public int Available => _inner.Available;

    /// <inheritdoc />
    public int Limit => _inner.Limit;

    /// <inheritdoc />
    public bool TryAcquire(string? key = null) => _inner.TryAcquire(key);

    /// <inheritdoc />
    public Task WaitAsync(string? key = null, CancellationToken ct = default) => _inner.WaitAsync(key, ct);
}
