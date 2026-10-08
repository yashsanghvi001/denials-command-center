using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace DenialsCommandCenter.Tests.Integration;

[Collection("postgres")]
public class IngestionFailureTests(PostgresFixture pg) : IAsyncLifetime
{
    private WebApplicationFactory<Program> _factory = null!;

    public Task InitializeAsync()
    {
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Denials", pg.ConnectionString);
            builder.UseSetting("Ingestion:DataDirectory", Path.Combine(TestData.Dir, "does-not-exist"));
            builder.UseSetting("Ingestion:Today", "2026-09-30");
            builder.UseSetting("Auth:SeedPassword", TestAuth.Password);
        });
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    [Fact]
    public async Task Api_starts_and_reports_a_problem_when_ingestion_fails()
    {
        Assert.Equal(HttpStatusCode.OK, (await _factory.CreateClient().GetAsync("/api/health")).StatusCode);

        var client = await TestAuth.LoginAsync(_factory, "manager");
        var response = await client.PostAsync("/api/ingestion/run", null);
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Contains("does-not-exist", problem.GetProperty("detail").GetString());
    }
}
