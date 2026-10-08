using System.Collections.Concurrent;
using System.Net;
using DenialsCommandCenter.Api.Configuration;

namespace DenialsCommandCenter.Api.Analysis;

// Process-wide guard around paid model calls. Identical requests that arrive while one is in flight share its
// answer, and after the provider refuses (quota or outage) calls pause until it is expected to accept again.
// A multi-instance deployment would keep both in Redis (SET NX lock, shared pause key) instead of memory.
public sealed class AiCallGuard(TimeProvider clock, GeminiOptions options)
{
    private readonly ConcurrentDictionary<string, Lazy<Task<string>>> _inFlight = new();
    private long _pausedUntilTicks;

    public DateTimeOffset? PausedUntil
    {
        get
        {
            var until = new DateTimeOffset(Interlocked.Read(ref _pausedUntilTicks), TimeSpan.Zero);
            return until > clock.GetUtcNow() ? until : null;
        }
    }

    public void Pause(GeminiUnavailableException failure)
    {
        if (failure.StatusCode is not (HttpStatusCode.TooManyRequests or >= HttpStatusCode.InternalServerError)) return;

        var now = clock.GetUtcNow();
        var until = failure.DailyQuotaExhausted
            ? new DateTimeOffset(now.UtcDateTime.Date.AddDays(1), TimeSpan.Zero)
            : now + (failure.RetryAfter ?? TimeSpan.FromSeconds(options.PauseAfterRefusalSeconds));

        long current;
        do
        {
            current = Interlocked.Read(ref _pausedUntilTicks);
            if (current >= until.UtcTicks) return;
        }
        while (Interlocked.CompareExchange(ref _pausedUntilTicks, until.UtcTicks, current) != current);
    }

    public Task<string> CallOnceAsync(string key, Func<Task<string>> call) =>
        _inFlight.GetOrAdd(key, _ => new Lazy<Task<string>>(() => RunAsync(key, call))).Value;

    private async Task<string> RunAsync(string key, Func<Task<string>> call)
    {
        try
        {
            return await call();
        }
        finally
        {
            _inFlight.TryRemove(key, out _);
        }
    }
}
