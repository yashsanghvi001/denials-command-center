using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using DenialsCommandCenter.Api.Configuration;

namespace DenialsCommandCenter.Api.Analysis;

public sealed class GeminiClient(HttpClient http, GeminiOptions options)
{
    public async Task<string> GenerateJsonAsync(string systemInstruction, string userContent, JsonObject responseSchema, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(options.BaseUrl, $"v1beta/models/{options.Model}:generateContent"));
        request.Headers.Add("x-goog-api-key", options.ApiKey);
        request.Content = JsonContent.Create(new JsonObject
        {
            ["systemInstruction"] = new JsonObject { ["parts"] = new JsonArray(new JsonObject { ["text"] = systemInstruction }) },
            ["contents"] = new JsonArray(new JsonObject
            {
                ["role"] = "user",
                ["parts"] = new JsonArray(new JsonObject { ["text"] = userContent }),
            }),
            ["generationConfig"] = new JsonObject
            {
                ["temperature"] = 0.1,
                ["responseMimeType"] = "application/json",
                ["responseSchema"] = responseSchema,
            },
        });

        using var response = await http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode) throw await UnavailableAsync(response, ct);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        if (!document.RootElement.TryGetProperty("candidates", out var candidates) || candidates.GetArrayLength() == 0)
            throw new InvalidDataException("Gemini returned no candidates.");
        if (!candidates[0].TryGetProperty("content", out var content)
            || !content.TryGetProperty("parts", out var parts)
            || parts.ValueKind != JsonValueKind.Array
            || parts.GetArrayLength() == 0)
            throw new InvalidDataException("Gemini returned no answer parts.");
        return parts[0].GetProperty("text").GetString()
            ?? throw new InvalidDataException("Gemini returned an empty answer.");
    }

    // Gemini explains a refusal in google.rpc details: RetryInfo.retryDelay ("41s") and QuotaFailure violations
    // whose quotaId names the exhausted quota (per-minute or per-day).
    private static async Task<GeminiUnavailableException> UnavailableAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var retryAfter = response.Headers.RetryAfter?.Delta;
        var dailyQuotaExhausted = false;
        try
        {
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            if (document.RootElement.TryGetProperty("error", out var error)
                && error.TryGetProperty("details", out var details) && details.ValueKind == JsonValueKind.Array)
            {
                foreach (var detail in details.EnumerateArray())
                {
                    if (detail.TryGetProperty("retryDelay", out var delay) && delay.ValueKind == JsonValueKind.String
                        && TryParseSeconds(delay.GetString(), out var seconds))
                        retryAfter ??= seconds;
                    if (detail.TryGetProperty("violations", out var violations) && violations.ValueKind == JsonValueKind.Array)
                        dailyQuotaExhausted |= violations.EnumerateArray().Any(violation =>
                            violation.TryGetProperty("quotaId", out var quotaId) && quotaId.ValueKind == JsonValueKind.String
                            && quotaId.GetString()?.Contains("PerDay", StringComparison.Ordinal) == true);
                }
            }
        }
        catch (JsonException)
        {
        }
        return new GeminiUnavailableException(response.StatusCode, retryAfter, dailyQuotaExhausted);
    }

    private static bool TryParseSeconds(string? value, out TimeSpan delay)
    {
        delay = default;
        if (value is null || !value.EndsWith('s')) return false;
        if (!double.TryParse(value[..^1], NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds)) return false;
        delay = TimeSpan.FromSeconds(seconds);
        return true;
    }
}
