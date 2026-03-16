namespace Philiprehberger.RateLimiter;

/// <summary>
/// A rate limiter that uses a sliding window algorithm divided into segments.
/// Provides smoother rate limiting than fixed windows by tracking counts across
/// multiple sub-segments. Thread-safe.
/// </summary>
public sealed class SlidingWindowLimiter : IRateLimiter
{
    private readonly int _limit;
    private readonly TimeSpan _window;
    private readonly int _segments;
    private readonly TimeSpan _segmentDuration;
    private readonly object _lock = new();
    private readonly Dictionary<string, SlidingState> _states = new();
    private readonly SlidingState _defaultState;

    /// <summary>
    /// Creates a new <see cref="SlidingWindowLimiter"/> instance.
    /// </summary>
    /// <param name="limit">The maximum number of permits per window.</param>
    /// <param name="window">The total duration of the sliding window.</param>
    /// <param name="segments">The number of segments to divide the window into. Defaults to 10.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="limit"/> is less than 1, <paramref name="window"/> is not positive,
    /// or <paramref name="segments"/> is less than 1.
    /// </exception>
    public SlidingWindowLimiter(int limit, TimeSpan window, int segments = 10)
    {
        if (limit < 1)
            throw new ArgumentOutOfRangeException(nameof(limit), limit, "Limit must be at least 1.");
        if (window <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(window), window, "Window must be positive.");
        if (segments < 1)
            throw new ArgumentOutOfRangeException(nameof(segments), segments, "Segments must be at least 1.");

        _limit = limit;
        _window = window;
        _segments = segments;
        _segmentDuration = TimeSpan.FromTicks(window.Ticks / segments);
        _defaultState = new SlidingState(segments);
    }

    /// <inheritdoc />
    public int Available
    {
        get
        {
            lock (_lock)
            {
                Slide(_defaultState);
                return _limit - GetTotal(_defaultState);
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
            var state = GetState(key);
            Slide(state);

            if (GetTotal(state) >= _limit)
                return false;

            state.Counts[state.CurrentSegment]++;
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

    private SlidingState GetState(string? key)
    {
        if (key is null)
            return _defaultState;

        if (!_states.TryGetValue(key, out var state))
        {
            state = new SlidingState(_segments);
            _states[key] = state;
        }

        return state;
    }

    private void Slide(SlidingState state)
    {
        var now = DateTime.UtcNow;
        var elapsed = now - state.LastSlide;
        var segmentsToSlide = (int)(elapsed.Ticks / _segmentDuration.Ticks);

        if (segmentsToSlide <= 0)
            return;

        // Clear expired segments
        var toClear = Math.Min(segmentsToSlide, _segments);
        for (var i = 0; i < toClear; i++)
        {
            state.CurrentSegment = (state.CurrentSegment + 1) % _segments;
            state.Counts[state.CurrentSegment] = 0;
        }

        state.LastSlide = state.LastSlide.Add(TimeSpan.FromTicks(segmentsToSlide * _segmentDuration.Ticks));
    }

    private static int GetTotal(SlidingState state)
    {
        var total = 0;
        foreach (var count in state.Counts)
            total += count;
        return total;
    }

    private sealed class SlidingState
    {
        public readonly int[] Counts;
        public int CurrentSegment;
        public DateTime LastSlide;

        public SlidingState(int segments)
        {
            Counts = new int[segments];
            CurrentSegment = 0;
            LastSlide = DateTime.UtcNow;
        }
    }
}
