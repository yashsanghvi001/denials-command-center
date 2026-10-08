using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace DenialsCommandCenter.Tests.Integration;

[Collection("postgres")]
public class AuthTests(PostgresFixture pg) : IAsyncLifetime
{
    private WebApplicationFactory<Program> _factory = null!;

    public Task InitializeAsync()
    {
        _factory = TestAuth.Factory(pg);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    [Fact]
    public async Task Health_is_public_and_everything_else_needs_a_session()
    {
        var client = _factory.CreateClient();

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/health")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/reconciliation")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/claims/GPP-2026-000230")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
    }

    [Theory]
    [InlineData("manager", "wrong-password")]
    [InlineData("nobody", TestAuth.Password)]
    public async Task Wrong_credentials_are_rejected(string username, string password)
    {
        var response = await _factory.CreateClient().PostAsJsonAsync("/api/auth/login", new { username, password });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_sets_an_http_only_strict_session_cookie()
    {
        var response = await _factory.CreateClient().PostAsJsonAsync("/api/auth/login", new { username = "Manager ", password = TestAuth.Password });

        response.EnsureSuccessStatusCode();
        var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie")).ToLowerInvariant();
        Assert.StartsWith("denials_session=", cookie);
        Assert.Contains("httponly", cookie);
        Assert.Contains("samesite=strict", cookie);
    }

    [Fact]
    public async Task Specialist_cannot_open_manager_pages()
    {
        var client = await TestAuth.LoginAsync(_factory, "priya");

        var me = JsonDocument.Parse(await client.GetStringAsync("/api/auth/me")).RootElement;
        Assert.Equal(("priya", "Specialist"), (me.GetProperty("username").GetString(), me.GetProperty("role").GetString()));
        foreach (var url in new[] { "/api/reconciliation", "/api/exceptions", "/api/denials", "/api/denials/summary", "/api/review-queue", "/api/evaluation", "/api/users" })
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync("/api/ingestion/run", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync("/api/evaluation/ai", null)).StatusCode);
    }

    [Fact]
    public async Task Manager_can_open_manager_pages()
    {
        var client = await TestAuth.LoginAsync(_factory, "manager");
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/reconciliation")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/denials/summary")).StatusCode);
    }

    [Fact]
    public async Task Logout_ends_the_session()
    {
        var client = await TestAuth.LoginAsync(_factory, "manager");

        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync("/api/auth/logout", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
    }
}
