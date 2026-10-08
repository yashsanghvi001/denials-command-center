using System.Security.Claims;
using DenialsCommandCenter.Api.Auth;
using DenialsCommandCenter.Api.Configuration;
using DenialsCommandCenter.Api.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace DenialsCommandCenter.Api.Endpoints;

public sealed record LoginRequest(string? Username, string? Password);

public sealed record CurrentUserResponse(string Username, string DisplayName, string Role);

public sealed record AuthOptionsResponse(bool PasswordReset);

public sealed record PasswordResetRequest(string? Username);

public sealed record PasswordResetConfirmation(string? Token, string? NewPassword);

public static class AuthEndpoints
{
    private const string ResetNotSetUp = "Password reset by email is not set up. Ask your administrator to reset your password.";
    private static readonly string UnknownUserHash = new PasswordHasher<UserRow>().HashPassword(null!, Guid.NewGuid().ToString());

    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/auth/login", async (LoginRequest request, DenialsDbContext db, HttpContext http) =>
        {
            var username = request.Username?.Trim().ToLowerInvariant() ?? "";
            var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Username == username);
            // An unknown username still pays for a full hash check, so response time does not reveal which usernames exist.
            var verified = new PasswordHasher<UserRow>().VerifyHashedPassword(user!, user?.PasswordHash ?? UnknownUserHash, request.Password ?? "");
            if (user is null || verified == PasswordVerificationResult.Failed)
                return Results.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Wrong username or password.");

            var identity = new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.Name, user.Username), new Claim(ClaimTypes.Role, user.Role), new Claim(UserClaims.DisplayName, user.DisplayName),
                    new Claim(SessionStamp.ClaimType, SessionStamp.For(user)),
                ],
                CookieAuthenticationDefaults.AuthenticationScheme);
            await http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
            return Results.Ok(new CurrentUserResponse(user.Username, user.DisplayName, user.Role));
        }).AllowAnonymous().RequireRateLimiting(RateLimits.Login);

        app.MapPost("/api/auth/logout", async (HttpContext http) =>
        {
            await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.NoContent();
        });

        app.MapGet("/api/auth/options", (GraphMailOptions mail) => Results.Ok(new AuthOptionsResponse(mail.IsConfigured)))
            .AllowAnonymous();

        // Always answers the same way, so the form cannot be used to find out which usernames exist.
        app.MapPost("/api/auth/password-reset", async (
            PasswordResetRequest request, DenialsDbContext db, GraphMailOptions mail, IMailSender sender, AuthOptions auth,
            AppOptions appOptions, ILoggerFactory loggers, CancellationToken ct) =>
        {
            if (!mail.IsConfigured) return Results.Problem(statusCode: StatusCodes.Status404NotFound, title: ResetNotSetUp);

            var username = request.Username?.Trim().ToLowerInvariant() ?? "";
            var user = await db.Users.SingleOrDefaultAsync(u => u.Username == username, ct);
            if (user?.Email is { Length: > 0 } email)
            {
                var token = PasswordResetTokens.Create();
                user.PasswordResetTokenHash = PasswordResetTokens.Hash(token);
                user.PasswordResetExpiresAt = DateTimeOffset.UtcNow.AddMinutes(auth.PasswordResetMinutes);
                await db.SaveChangesAsync(ct);

                var link = $"{appOptions.PublicUrl.TrimEnd('/')}/reset-password?token={token}";
                try
                {
                    await sender.SendAsync(email, "Reset your Denials Command Center password", $"""
                        Hello {user.DisplayName},

                        Use this link to choose a new password. It works once and expires in {auth.PasswordResetMinutes} minutes:

                        {link}

                        If you did not ask for this, you can ignore this email; your password has not changed.
                        """, ct);
                }
                catch (HttpRequestException ex)
                {
                    loggers.CreateLogger(nameof(AuthEndpoints)).LogError("Password reset email for {Username} could not be sent: {Reason}", username, ex.Message);
                }
            }
            return Results.Accepted();
        }).AllowAnonymous().RequireRateLimiting(RateLimits.Login);

        app.MapPost("/api/auth/password-reset/confirm", async (
            PasswordResetConfirmation request, DenialsDbContext db, GraphMailOptions mail, AuthOptions auth, IMemoryCache cache, CancellationToken ct) =>
        {
            if (!mail.IsConfigured) return Results.Problem(statusCode: StatusCodes.Status404NotFound, title: ResetNotSetUp);
            if (PasswordPolicy.Problem(request.NewPassword, auth) is { } problem)
                return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: problem);

            var tokenHash = string.IsNullOrWhiteSpace(request.Token) ? null : PasswordResetTokens.Hash(request.Token.Trim());
            var user = tokenHash is null ? null : await db.Users.SingleOrDefaultAsync(u => u.PasswordResetTokenHash == tokenHash, ct);
            if (user is null || user.PasswordResetExpiresAt < DateTimeOffset.UtcNow)
                return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "This reset link is invalid or has expired. Ask for a new one.");

            user.PasswordHash = new PasswordHasher<UserRow>().HashPassword(user, request.NewPassword!);
            user.PasswordResetTokenHash = null;
            user.PasswordResetExpiresAt = null;
            await db.SaveChangesAsync(ct);
            // The new password hash changes the session stamp; dropping the cached stamp signs out old sessions at once.
            SessionStamp.Forget(cache, user.Username);
            return Results.NoContent();
        }).AllowAnonymous().RequireRateLimiting(RateLimits.Login);

        app.MapGet("/api/auth/me", (ClaimsPrincipal user) => Results.Ok(new CurrentUserResponse(
            user.Username(), user.FindFirstValue(UserClaims.DisplayName) ?? "", user.FindFirstValue(ClaimTypes.Role) ?? "")));
    }
}
