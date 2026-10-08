using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using DenialsCommandCenter.Api.Auth;
using DenialsCommandCenter.Api.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DenialsCommandCenter.Tests.Integration;

[Collection("postgres")]
public class PasswordResetTests(PostgresFixture pg) : IAsyncLifetime
{
    private const string Username = "anjali";
    private const string NewPassword = "brighter-day-42";
    private readonly RecordingMailSender _mail = new();
    private WebApplicationFactory<Program> _factory = null!;

    public Task InitializeAsync()
    {
        _factory = TestAuth.Factory(pg).WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Graph:TenantId", "tenant");
            builder.UseSetting("Graph:ClientId", "client");
            builder.UseSetting("Graph:ClientSecret", "secret");
            builder.UseSetting("Graph:SenderAddress", "denials@example.test");
            builder.UseSetting($"Auth:Emails:{Username}", "anjali@example.test");
            builder.ConfigureTestServices(services => services.AddSingleton<IMailSender>(_mail));
        });
        return Task.CompletedTask;
    }

    // Other test classes sign in as this user, so the original password is always put back.
    public async Task DisposeAsync()
    {
        await using var db = pg.NewDb();
        var user = await db.Users.SingleAsync(u => u.Username == Username);
        user.PasswordHash = new PasswordHasher<UserRow>().HashPassword(user, TestAuth.Password);
        user.PasswordResetTokenHash = null;
        user.PasswordResetExpiresAt = null;
        await db.SaveChangesAsync();
        await _factory.DisposeAsync();
    }

    private async Task<string> RequestResetTokenAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/auth/password-reset", new { username = Username });
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var message = Assert.Single(_mail.Sent);
        Assert.Equal("anjali@example.test", message.To);
        return Regex.Match(message.Body, @"/reset-password\?token=([A-Za-z0-9_-]+)").Groups[1].Value;
    }

    [Fact]
    public async Task Reset_link_sets_a_new_password_once()
    {
        var client = _factory.CreateClient();
        var options = JsonDocument.Parse(await client.GetStringAsync("/api/auth/options")).RootElement;
        Assert.True(options.GetProperty("passwordReset").GetBoolean());

        var token = await RequestResetTokenAsync(client);
        var weak = await client.PostAsJsonAsync("/api/auth/password-reset/confirm", new { token, newPassword = "short" });
        var changed = await client.PostAsJsonAsync("/api/auth/password-reset/confirm", new { token, newPassword = NewPassword });
        var reused = await client.PostAsJsonAsync("/api/auth/password-reset/confirm", new { token, newPassword = NewPassword });

        Assert.Equal((HttpStatusCode.BadRequest, HttpStatusCode.NoContent, HttpStatusCode.BadRequest), (weak.StatusCode, changed.StatusCode, reused.StatusCode));
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/auth/login", new { username = Username, password = NewPassword })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/login", new { username = Username, password = TestAuth.Password })).StatusCode);
    }

    [Fact]
    public async Task Expired_link_is_refused()
    {
        var client = _factory.CreateClient();
        var token = await RequestResetTokenAsync(client);
        await using (var db = pg.NewDb())
            await db.Users.Where(u => u.Username == Username)
                .ExecuteUpdateAsync(u => u.SetProperty(x => x.PasswordResetExpiresAt, DateTimeOffset.UtcNow.AddMinutes(-1)));

        var response = await client.PostAsJsonAsync("/api/auth/password-reset/confirm", new { token, newPassword = NewPassword });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Unknown_user_gets_the_same_answer_and_no_email()
    {
        var response = await _factory.CreateClient().PostAsJsonAsync("/api/auth/password-reset", new { username = "nobody" });

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Empty(_mail.Sent);
    }

    [Fact]
    public async Task Reset_is_unavailable_until_graph_is_configured()
    {
        await using var factory = TestAuth.Factory(pg);
        var client = factory.CreateClient();

        var options = JsonDocument.Parse(await client.GetStringAsync("/api/auth/options")).RootElement;
        var request = await client.PostAsJsonAsync("/api/auth/password-reset", new { username = Username });
        var confirm = await client.PostAsJsonAsync("/api/auth/password-reset/confirm", new { token = "x", newPassword = NewPassword });

        Assert.False(options.GetProperty("passwordReset").GetBoolean());
        Assert.Equal((HttpStatusCode.NotFound, HttpStatusCode.NotFound), (request.StatusCode, confirm.StatusCode));
    }

    private sealed class RecordingMailSender : IMailSender
    {
        public List<(string To, string Subject, string Body)> Sent { get; } = [];

        public Task SendAsync(string to, string subject, string body, CancellationToken ct)
        {
            Sent.Add((to, subject, body));
            return Task.CompletedTask;
        }
    }
}
