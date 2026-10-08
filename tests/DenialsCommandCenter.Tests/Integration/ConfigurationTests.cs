using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;

namespace DenialsCommandCenter.Tests.Integration;

[Collection("postgres")]
public class ConfigurationTests(PostgresFixture pg)
{
    [Fact]
    public async Task An_invalid_limit_stops_startup()
    {
        await using var factory = TestAuth.Factory(pg).WithWebHostBuilder(builder => builder.UseSetting("Limits:MaxPageSize", "0"));

        var failure = Assert.Throws<OptionsValidationException>(() => factory.CreateClient());

        Assert.Contains(nameof(Api.Configuration.LimitsOptions.MaxPageSize), failure.Message);
    }

    // An empty value binds as absent, the same as a key missing from appsettings.json.
    [Theory]
    [InlineData("Gemini:DailyCallLimit")]
    [InlineData("Limits:ExpensiveQueueLength")]
    public async Task A_missing_zero_allowed_setting_stops_startup(string key)
    {
        await using var factory = TestAuth.Factory(pg).WithWebHostBuilder(builder => builder.UseSetting(key, ""));

        var failure = Assert.Throws<OptionsValidationException>(() => factory.CreateClient());

        Assert.Contains(key.Split(':')[1], failure.Message);
    }

    [Fact]
    public async Task A_missing_ingestion_date_stops_startup()
    {
        await using var factory = TestAuth.Factory(pg).WithWebHostBuilder(builder => builder.UseSetting("Ingestion:Today", ""));

        var failure = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());

        Assert.Contains("Ingestion:Today", failure.Message);
    }

    [Fact]
    public async Task Page_size_limit_comes_from_configuration()
    {
        await using var factory = TestAuth.Factory(pg).WithWebHostBuilder(builder => builder.UseSetting("Limits:MaxPageSize", "50"));
        var client = await TestAuth.LoginAsync(factory, "manager");

        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/worklist?pageSize=60")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/worklist?pageSize=50")).StatusCode);
    }
}
