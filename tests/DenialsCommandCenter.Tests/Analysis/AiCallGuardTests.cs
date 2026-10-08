using System.Net;
using DenialsCommandCenter.Api.Analysis;

namespace DenialsCommandCenter.Tests.Analysis;

public class AiCallGuardTests
{
    private static readonly DateTimeOffset Noon = new(2026, 10, 8, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Identical_calls_in_flight_share_one_model_call()
    {
        var guard = new AiCallGuard(new FixedClock(Noon), TestOptions.Gemini());
        var answer = new TaskCompletionSource<string>();
        var calls = 0;
        Task<string> Call()
        {
            Interlocked.Increment(ref calls);
            return answer.Task;
        }

        var first = guard.CallOnceAsync("facts-1", Call);
        var second = guard.CallOnceAsync("facts-1", Call);
        answer.SetResult("{}");

        Assert.Equal(("{}", "{}"), (await first, await second));
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task Different_facts_and_later_calls_are_not_shared()
    {
        var guard = new AiCallGuard(new FixedClock(Noon), TestOptions.Gemini());
        var calls = 0;
        Task<string> Call()
        {
            Interlocked.Increment(ref calls);
            return Task.FromResult("{}");
        }

        await guard.CallOnceAsync("facts-1", Call);
        await guard.CallOnceAsync("facts-2", Call);
        await guard.CallOnceAsync("facts-1", Call);

        Assert.Equal(3, calls);
    }

    [Fact]
    public async Task A_caller_joining_an_in_flight_call_receives_its_failure()
    {
        var guard = new AiCallGuard(new FixedClock(Noon), TestOptions.Gemini());
        var answer = new TaskCompletionSource<string>();

        var first = guard.CallOnceAsync("facts-1", () => answer.Task);
        var second = guard.CallOnceAsync("facts-1", () => Task.FromResult("{}"));
        answer.SetException(new InvalidOperationException("model down"));

        Assert.Equal("model down", (await Assert.ThrowsAsync<InvalidOperationException>(() => first)).Message);
        Assert.Equal("model down", (await Assert.ThrowsAsync<InvalidOperationException>(() => second)).Message);
    }

    [Fact]
    public async Task A_failed_call_is_not_remembered()
    {
        var guard = new AiCallGuard(new FixedClock(Noon), TestOptions.Gemini());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            guard.CallOnceAsync("facts-1", () => Task.FromException<string>(new InvalidOperationException())));

        Assert.Equal("{}", await guard.CallOnceAsync("facts-1", () => Task.FromResult("{}")));
    }

    [Fact]
    public void Per_minute_refusal_pauses_for_the_retry_delay()
    {
        var clock = new FixedClock(Noon);
        var guard = new AiCallGuard(clock, TestOptions.Gemini());

        guard.Pause(new GeminiUnavailableException(HttpStatusCode.TooManyRequests, TimeSpan.FromSeconds(41), dailyQuotaExhausted: false));

        Assert.Equal(Noon.AddSeconds(41), guard.PausedUntil);
        clock.Now = Noon.AddSeconds(42);
        Assert.Null(guard.PausedUntil);
    }

    [Fact]
    public void Daily_quota_refusal_pauses_until_the_next_utc_day()
    {
        var guard = new AiCallGuard(new FixedClock(Noon), TestOptions.Gemini());

        guard.Pause(new GeminiUnavailableException(HttpStatusCode.TooManyRequests, TimeSpan.FromSeconds(41), dailyQuotaExhausted: true));

        Assert.Equal(new DateTimeOffset(2026, 10, 9, 0, 0, 0, TimeSpan.Zero), guard.PausedUntil);
    }

    [Fact]
    public void Outage_without_a_hint_pauses_one_minute_and_a_shorter_pause_never_shortens_it()
    {
        var guard = new AiCallGuard(new FixedClock(Noon), TestOptions.Gemini());

        guard.Pause(new GeminiUnavailableException(HttpStatusCode.ServiceUnavailable, null, dailyQuotaExhausted: false));
        guard.Pause(new GeminiUnavailableException(HttpStatusCode.TooManyRequests, TimeSpan.FromSeconds(5), dailyQuotaExhausted: false));

        Assert.Equal(Noon.AddMinutes(1), guard.PausedUntil);
    }

    [Fact]
    public void Client_errors_do_not_pause()
    {
        var guard = new AiCallGuard(new FixedClock(Noon), TestOptions.Gemini());

        guard.Pause(new GeminiUnavailableException(HttpStatusCode.BadRequest, null, dailyQuotaExhausted: false));

        Assert.Null(guard.PausedUntil);
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;

        public override DateTimeOffset GetUtcNow() => Now;
    }
}
