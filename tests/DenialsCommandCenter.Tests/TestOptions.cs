using DenialsCommandCenter.Api.Configuration;

namespace DenialsCommandCenter.Tests;

// The appsettings.json defaults, written out for tests that build services by hand.
public static class TestOptions
{
    public static GeminiOptions Gemini(string? apiKey = "test-key", int dailyCallLimit = 200) => new()
    {
        ApiKey = apiKey,
        BaseUrl = new Uri("https://gemini.example/"),
        Model = "gemini-test",
        DailyCallLimit = dailyCallLimit,
        TimeoutSeconds = 30,
        PauseAfterRefusalSeconds = 60,
        EvaluationStopAfterUnavailable = 3,
    };

    public static AuthOptions Auth { get; } = new()
    {
        SessionHours = 8,
        SessionStampCacheSeconds = 30,
        PasswordResetMinutes = 30,
        PasswordMinLength = 8,
        PasswordMaxLength = 128,
    };
}
