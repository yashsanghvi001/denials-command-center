using System.Text;
using System.Text.Json.Nodes;
using DenialsCommandCenter.Api.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace DenialsCommandCenter.Tests.Configuration;

public class OptionsValidationTests
{
    private static readonly string AppSettingsPath = Path.Combine(TestData.Dir, "..", "src", "DenialsCommandCenter.Api", "appsettings.json");

    private static ServiceProvider Services(Dictionary<string, string?>? overrides = null, string? removedKey = null)
    {
        var settings = JsonNode.Parse(File.ReadAllText(AppSettingsPath))!;
        if (removedKey?.Split(':') is [var section, var key])
            settings[section]!.AsObject().Remove(key);
        var configuration = new ConfigurationBuilder()
            .AddJsonStream(new MemoryStream(Encoding.UTF8.GetBytes(settings.ToJsonString())))
            .AddInMemoryCollection(overrides ?? [])
            .Build();
        var services = new ServiceCollection().AddSingleton<IConfiguration>(configuration);
        services.AddValidatedOptions<AppOptions>(AppOptions.Section);
        services.AddValidatedOptions<AuthOptions>(AuthOptions.Section);
        services.AddValidatedOptions<GeminiOptions>(GeminiOptions.Section);
        services.AddValidatedOptions<GraphMailOptions>(GraphMailOptions.Section);
        services.AddValidatedOptions<LimitsOptions>(LimitsOptions.Section);
        return services.BuildServiceProvider();
    }

    [Fact]
    public void Shipped_defaults_are_valid_and_keep_the_documented_values()
    {
        using var services = Services();
        services.GetRequiredService<IStartupValidator>().Validate();

        var gemini = services.GetRequiredService<GeminiOptions>();
        var limits = services.GetRequiredService<LimitsOptions>();
        Assert.Equal(("gemini-2.5-flash", new Uri("https://generativelanguage.googleapis.com/"), 200), (gemini.Model, gemini.BaseUrl, gemini.DailyCallLimit));
        Assert.Equal((10, 4, 50, 25, 100), (limits.LoginAttemptsPerMinute, limits.ExpensiveConcurrentRequests, limits.ExpensiveQueueLength, limits.DefaultPageSize, limits.MaxPageSize));
        Assert.False(gemini.IsConfigured);
        Assert.False(services.GetRequiredService<GraphMailOptions>().IsConfigured);
    }

    [Theory]
    [InlineData("Gemini:Model", "")]
    [InlineData("Gemini:BaseUrl", "https://generativelanguage.googleapis.com/v1beta")]
    [InlineData("Graph:ApiBaseUrl", "graph.microsoft.com/v1.0/")]
    [InlineData("Limits:LoginAttemptsPerMinute", "0")]
    [InlineData("Limits:ExpensiveConcurrentRequests", "1000000")]
    [InlineData("Limits:DefaultPageSize", "101")]
    [InlineData("Limits:MaxRequestBodyBytes", "100")]
    [InlineData("Auth:PasswordMinLength", "128")]
    [InlineData("Auth:SessionHours", "0")]
    [InlineData("App:PublicUrl", "")]
    public void A_missing_or_out_of_range_setting_fails_validation(string key, string value)
    {
        using var services = Services(new() { [key] = value });

        Assert.Throws<OptionsValidationException>(() => services.GetRequiredService<IStartupValidator>().Validate());
    }

    // Zero is a valid value for these two, so only a presence check catches a deleted or misspelt key.
    [Theory]
    [InlineData("Gemini:DailyCallLimit")]
    [InlineData("Limits:ExpensiveQueueLength")]
    [InlineData("Limits:LoginAttemptsPerMinute")]
    public void A_setting_deleted_from_appsettings_fails_validation(string key)
    {
        using var services = Services(removedKey: key);

        var failure = Assert.Throws<OptionsValidationException>(() => services.GetRequiredService<IStartupValidator>().Validate());
        Assert.Contains(key.Split(':')[1], failure.Message);
    }
}
