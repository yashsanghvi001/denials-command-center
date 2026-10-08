using System.Net;

namespace DenialsCommandCenter.Api.Analysis;

public sealed class GeminiUnavailableException(HttpStatusCode statusCode, TimeSpan? retryAfter, bool dailyQuotaExhausted)
    : HttpRequestException($"Gemini answered {(int)statusCode}.", null, statusCode)
{
    public TimeSpan? RetryAfter { get; } = retryAfter;

    public bool DailyQuotaExhausted { get; } = dailyQuotaExhausted;
}
