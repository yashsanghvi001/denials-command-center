using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using DenialsCommandCenter.Api.Analysis;

namespace DenialsCommandCenter.Tests.Analysis;

public class GeminiClientTests
{
    private const string DailyQuotaBody = """
        {"error":{"code":429,"status":"RESOURCE_EXHAUSTED","details":[
          {"@type":"type.googleapis.com/google.rpc.QuotaFailure","violations":[{"quotaId":"GenerateRequestsPerDayPerProjectPerModel-FreeTier"}]},
          {"@type":"type.googleapis.com/google.rpc.RetryInfo","retryDelay":"41s"}]}}
        """;

    private static Task<string> CallAsync(HttpStatusCode status, string body) =>
        new GeminiClient(new HttpClient(new StubHandler(status, body)), TestOptions.Gemini())
            .GenerateJsonAsync("system", "facts", new JsonObject(), CancellationToken.None);

    [Fact]
    public async Task Daily_quota_refusal_is_recognised_with_its_retry_delay()
    {
        var failure = await Assert.ThrowsAsync<GeminiUnavailableException>(() => CallAsync(HttpStatusCode.TooManyRequests, DailyQuotaBody));

        Assert.Equal((HttpStatusCode.TooManyRequests, true, TimeSpan.FromSeconds(41)), (failure.StatusCode, failure.DailyQuotaExhausted, failure.RetryAfter));
    }

    [Fact]
    public async Task Per_minute_refusal_is_not_a_daily_quota()
    {
        const string body = """{"error":{"code":429,"details":[{"@type":"type.googleapis.com/google.rpc.QuotaFailure","violations":[{"quotaId":"GenerateRequestsPerMinutePerProjectPerModel-FreeTier"}]}]}}""";

        var failure = await Assert.ThrowsAsync<GeminiUnavailableException>(() => CallAsync(HttpStatusCode.TooManyRequests, body));

        Assert.False(failure.DailyQuotaExhausted);
        Assert.Null(failure.RetryAfter);
    }

    [Fact]
    public async Task Server_error_with_a_non_json_body_is_still_reported()
    {
        var failure = await Assert.ThrowsAsync<GeminiUnavailableException>(() => CallAsync(HttpStatusCode.ServiceUnavailable, "<html>down</html>"));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, failure.StatusCode);
    }

    [Fact]
    public async Task Unexpected_shapes_in_the_refusal_details_still_give_a_typed_failure()
    {
        const string body = """{"error":{"code":429,"details":[{"retryDelay":41,"violations":[{"quotaId":7}]}]}}""";

        var failure = await Assert.ThrowsAsync<GeminiUnavailableException>(() => CallAsync(HttpStatusCode.TooManyRequests, body));

        Assert.Equal((false, null), (failure.DailyQuotaExhausted, failure.RetryAfter));
    }

    private sealed class StubHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
    }
}
