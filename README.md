# Philiprehberger.RateLimiter

[![CI](https://github.com/philiprehberger/dotnet-rate-limiter/actions/workflows/ci.yml/badge.svg)](https://github.com/philiprehberger/dotnet-rate-limiter/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/Philiprehberger.RateLimiter.svg)](https://www.nuget.org/packages/Philiprehberger.RateLimiter)
[![License](https://img.shields.io/github/license/philiprehberger/dotnet-rate-limiter)](LICENSE)

In-memory rate limiting with fixed window, sliding window, and token bucket algorithms.

## Installation

```bash
dotnet add package Philiprehberger.RateLimiter
```

## Usage

```csharp
using Philiprehberger.RateLimiter;
```

### Token Bucket

Tokens refill at a steady rate up to a maximum capacity. Good for smoothing bursts.

```csharp
var limiter = new TokenBucketLimiter(
    capacity: 10,
    refillRate: 5,
    refillInterval: TimeSpan.FromSeconds(1));

if (limiter.TryAcquire())
{
    // proceed with operation
}
```

### Fixed Window

Permits reset at the start of each time window.

```csharp
var limiter = new FixedWindowLimiter(
    limit: 100,
    window: TimeSpan.FromMinutes(1));

if (limiter.TryAcquire())
{
    // proceed with operation
}
```

### Sliding Window

Divides the window into segments for smoother rate limiting than fixed windows.

```csharp
var limiter = new SlidingWindowLimiter(
    limit: 100,
    window: TimeSpan.FromMinutes(1),
    segments: 10);

if (limiter.TryAcquire())
{
    // proceed with operation
}
```

### Async Wait

All limiters support async waiting until a permit is available:

```csharp
await limiter.WaitAsync(ct: cancellationToken);
// permit acquired
```

### Per-Key Rate Limiting

Use keys to apply independent rate limits per user, IP, or API endpoint:

```csharp
if (limiter.TryAcquire(key: "user-42"))
{
    // user-42 has permits remaining
}
```

### Fluent Builder

```csharp
var limiter = RateLimiterBuilder.Create()
    .TokenBucket(capacity: 10, refillRate: 5, per: TimeSpan.FromSeconds(1))
    .PerKey()
    .Build();
```

## API

### `IRateLimiter`

| Member | Description |
|--------|-------------|
| `Available` | Number of permits currently available |
| `Limit` | Maximum number of permits |
| `TryAcquire(key?)` | Acquires a permit without blocking. Returns `true` if successful |
| `WaitAsync(key?, ct)` | Waits asynchronously until a permit is available |

### `TokenBucketLimiter`

| Constructor Parameter | Description |
|----------------------|-------------|
| `capacity` | Maximum number of tokens |
| `refillRate` | Tokens added per refill interval |
| `refillInterval` | Time between refills |

### `FixedWindowLimiter`

| Constructor Parameter | Description |
|----------------------|-------------|
| `limit` | Maximum permits per window |
| `window` | Window duration |

### `SlidingWindowLimiter`

| Constructor Parameter | Description |
|----------------------|-------------|
| `limit` | Maximum permits per window |
| `window` | Window duration |
| `segments` | Number of sub-segments (default 10) |

### `RateLimiterBuilder`

| Method | Description |
|--------|-------------|
| `Create()` | Creates a new builder |
| `TokenBucket(capacity, refillRate, per)` | Configures token bucket algorithm |
| `FixedWindow(limit, window)` | Configures fixed window algorithm |
| `SlidingWindow(limit, window, segments)` | Configures sliding window algorithm |
| `PerKey()` | Enables per-key partitioning |
| `Build()` | Builds the configured rate limiter |

## Development

```bash
dotnet build src/Philiprehberger.RateLimiter.csproj --configuration Release
```

## License

MIT
