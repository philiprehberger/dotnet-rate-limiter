namespace Philiprehberger.RateLimiter;

/// <summary>
/// Defines a rate limiter that controls the rate of operations.
/// </summary>
public interface IRateLimiter
{
    /// <summary>
    /// Gets the number of permits currently available.
    /// </summary>
    int Available { get; }

    /// <summary>
    /// Gets the maximum number of permits allowed.
    /// </summary>
    int Limit { get; }

    /// <summary>
    /// Attempts to acquire a permit without blocking.
    /// </summary>
    /// <param name="key">An optional key for partitioned rate limiting.</param>
    /// <returns><c>true</c> if a permit was acquired; otherwise, <c>false</c>.</returns>
    bool TryAcquire(string? key = null);

    /// <summary>
    /// Waits asynchronously until a permit is available.
    /// </summary>
    /// <param name="key">An optional key for partitioned rate limiting.</param>
    /// <param name="ct">A cancellation token to cancel the wait.</param>
    /// <returns>A task that completes when a permit has been acquired.</returns>
    Task WaitAsync(string? key = null, CancellationToken ct = default);
}
