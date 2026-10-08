using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace DenialsCommandCenter.Tests.Integration;

public static class TestAuth
{
    public const string Password = "test-password";

    public static WebApplicationFactory<Program> Factory(PostgresFixture pg) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Denials", pg.ConnectionString);
            builder.UseSetting("Ingestion:DataDirectory", TestData.Dir);
            builder.UseSetting("Ingestion:Today", "2026-09-30");
            builder.UseSetting("Gemini:ApiKey", "");
            builder.UseSetting("Auth:SeedPassword", Password);
        });

    public static async Task<HttpClient> LoginAsync(WebApplicationFactory<Program> factory, string username)
    {
        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new { username, password = Password });
        response.EnsureSuccessStatusCode();
        return client;
    }
}
