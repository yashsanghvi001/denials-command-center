using System.Net;
using System.Net.Http.Json;
using DenialsCommandCenter.Api.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace DenialsCommandCenter.Tests.Integration;

[Collection("postgres")]
public class SecurityTests(PostgresFixture pg) : IAsyncLifetime
{
    private static readonly string[] Injections =
    [
        "' OR '1'='1", "'; DROP TABLE \"Claims\"; --", "%' UNION SELECT \"PasswordHash\" FROM \"Users\" --", @"\", "%", "_", "1; DELETE FROM \"WorkItems\"",
    ];

    private WebApplicationFactory<Program> _factory = null!;

    public async Task InitializeAsync()
    {
        _factory = TestAuth.Factory(pg);
        await TestAuth.LoginAsync(_factory, "manager");
    }

    public async Task DisposeAsync()
    {
        await using var db = pg.NewDb();
        var hasher = new PasswordHasher<Api.Data.UserRow>();
        foreach (var user in await db.Users.Where(u => u.Username == "priya" || u.Username == "karan").ToListAsync())
        {
            user.PasswordHash = hasher.HashPassword(user, TestAuth.Password);
            user.Role = Roles.Specialist;
        }
        await db.SaveChangesAsync();
        await _factory.DisposeAsync();
    }

    [Fact]
    public void Only_sign_in_and_health_endpoints_are_reachable_without_a_session()
    {
        var anonymous = _factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(e => e.Metadata.GetMetadata<IAllowAnonymous>() is not null)
            .Select(e => e.RoutePattern.RawText!)
            .Order()
            .ToList();

        Assert.All(anonymous, route => Assert.True(route == "/api/health" || route.StartsWith("/api/auth/", StringComparison.Ordinal), route));
        Assert.DoesNotContain("/api/auth/me", anonymous);
    }

    [Fact]
    public async Task Injection_payloads_are_treated_as_plain_text_and_change_nothing()
    {
        var client = await TestAuth.LoginAsync(_factory, "manager");
        await using var before = pg.NewDb();
        var counts = (await before.Claims.CountAsync(), await before.WorkItems.CountAsync(), await before.Users.CountAsync());

        foreach (var payload in Injections)
        {
            var value = Uri.EscapeDataString(payload);
            foreach (var url in new[]
            {
                $"/api/worklist?search={value}", $"/api/worklist?status={value}", $"/api/worklist?assignee={value}", $"/api/worklist?sort={value}",
                $"/api/exceptions?search={value}", $"/api/exceptions?kind={value}", $"/api/exceptions?severity={value}",
                $"/api/denials?payerId={value}", $"/api/denials?rootCause={value}", $"/api/claims/{value}", $"/api/denials/{value}",
            })
            {
                var status = (await client.GetAsync(url)).StatusCode;
                Assert.True(status is HttpStatusCode.OK or HttpStatusCode.BadRequest or HttpStatusCode.NotFound, $"{url} -> {status}");
            }
        }

        var literal = await client.GetAsync($"/api/worklist?search={Uri.EscapeDataString("%")}");
        Assert.Contains("\"totalItems\":0", await literal.Content.ReadAsStringAsync());
        await using var after = pg.NewDb();
        Assert.Equal(counts, (await after.Claims.CountAsync(), await after.WorkItems.CountAsync(), await after.Users.CountAsync()));
    }

    [Fact]
    public async Task Changing_a_password_ends_every_existing_session()
    {
        var client = await TestAuth.LoginAsync(_factory, "priya");
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/auth/me")).StatusCode);

        await using (var db = pg.NewDb())
        {
            var priya = await db.Users.SingleAsync(u => u.Username == "priya");
            priya.PasswordHash = new PasswordHasher<Api.Data.UserRow>().HashPassword(priya, "a-new-password-1");
            await db.SaveChangesAsync();
        }
        SessionStamp.Forget(_factory.Services.GetRequiredService<IMemoryCache>(), "priya");

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/worklist")).StatusCode);
    }

    [Fact]
    public async Task Changing_a_role_ends_the_session_that_carried_the_old_role()
    {
        var client = await TestAuth.LoginAsync(_factory, "karan");

        await using (var db = pg.NewDb())
        {
            await db.Users.Where(u => u.Username == "karan").ExecuteUpdateAsync(u => u.SetProperty(x => x.Role, Roles.Manager));
        }
        SessionStamp.Forget(_factory.Services.GetRequiredService<IMemoryCache>(), "karan");

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task The_login_attempt_over_the_per_minute_limit_is_refused()
    {
        await using var factory = TestAuth.Factory(pg);
        var client = factory.CreateClient();

        for (var attempt = 0; attempt < 10; attempt++)
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/login", new { username = "manager", password = "wrong" })).StatusCode);

        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.PostAsJsonAsync("/api/auth/login", new { username = "manager", password = TestAuth.Password })).StatusCode);
    }

    [Fact]
    public async Task Kestrel_refuses_request_bodies_over_the_configured_size()
    {
        // The in-memory test server does not enforce Kestrel limits (an oversized note gets 400 from note validation, not 413),
        // so this proves the configured limit reaches Kestrel, which answers 413 above it.
        await using var factory = TestAuth.Factory(pg).WithWebHostBuilder(builder => builder.UseSetting("Limits:MaxRequestBodyBytes", "4096"));
        _ = factory.Server;

        var kestrel = factory.Services.GetRequiredService<IOptions<KestrelServerOptions>>().Value;

        Assert.Equal(4096, kestrel.Limits.MaxRequestBodySize);
    }
}
