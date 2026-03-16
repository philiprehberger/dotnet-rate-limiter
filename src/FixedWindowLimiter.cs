namespace Philiprehberger.RateLimiter;

/// <summary>
/// A rate limiter that uses a fixed time window. Permits are reset at the start
/// of each window. Thread-safe.
/// </summary>
public sealed class FixedWindowLimiter : IRateLimiter
{
    private readonly int _limit;
    private readonly TimeSpan _window;
    private readonly object _lock = new();
    private readonly Dictionary<string, WindowState> _windows = new();
    private readonly WindowState _defaultWindow;

    /// <summary>
    /// Creates a new <see cref="FixedWindowLimiter"/> instance.
    /// </summary>
    /// <param name="limit">The maximum number of permits per window.</param>
    /// <param name="window">The duration of each window.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="limit"/> is less than 1 or <paramref name="window"/> is not positive.
    /// </exception>
    public FixedWindowLimiter(int limit, TimeSpan window)
    {
        if (limit < 1)
            throw new ArgumentOutOfRangeException(nameof(limit), limit, "Limit must be at least 1.");
        if (window <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(window), window, "Window must be positive.");

        _limit = limit;
        _window = window;
        _defaultWindow = new WindowState();
    }

    /// <inheritdoc />
    public int Available
    {
        get
        {
            lock (_lock)
            {
                ResetIfExpired(_defaultWindow);
                return _limit - _defaultWindow.Count;
            }
        }
    }

    /// <inheritdoc />
    public int Limit => _limit;

    /// <inheritdoc />
    public bool TryAcquire(string? key = null)
    {
        lock (_lock)
        {
            var state = GetWindow(key);
            ResetIfExpired(state);

            if (state.Count >= _limit)
                return false;

            state.Count++;
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

    private WindowState GetWindow(string? key)
    {
        if (key is null)
            return _defaultWindow;

        if (!_windows.TryGetValue(key, out var state))
        {
            state = new WindowState();
            _windows[key] = state;
        }

        return state;
    }

    private void ResetIfExpired(WindowState state)
    {
        var now = DateTime.UtcNow;
        if (now - state.WindowStart >= _window)
        {
            state.WindowStart = now;
            state.Count = 0;
        }
    }

    private sealed class WindowState
    {
        public DateTime WindowStart = DateTime.UtcNow;
        public int Count;
    }
}
