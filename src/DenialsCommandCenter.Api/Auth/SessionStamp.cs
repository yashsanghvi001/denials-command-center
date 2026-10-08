using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using DenialsCommandCenter.Api.Configuration;
using DenialsCommandCenter.Api.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace DenialsCommandCenter.Api.Auth;

// A session cookie lives for hours, so it carries a stamp of the user's password hash and role. A password reset,
// a role change or a removed account changes the stamp, and every older session is rejected on its next request.
// The current stamp is cached briefly so a busy API does not query the users table on every call.
public static class SessionStamp
{
    public const string ClaimType = "session_stamp";

    public static string For(UserRow user) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{user.Role}|{user.PasswordHash}")));

    public static void Forget(IMemoryCache cache, string username) => cache.Remove(CacheKey(username));

    public static async Task ValidateAsync(CookieValidatePrincipalContext context)
    {
        var username = context.Principal?.Identity?.Name;
        var stamp = context.Principal?.FindFirstValue(ClaimType);
        if (username is not null && stamp is not null && stamp == await CurrentStampAsync(context.HttpContext, username)) return;

        context.RejectPrincipal();
        await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    }

    private static string CacheKey(string username) => $"session-stamp:{username}";

    private static async Task<string> CurrentStampAsync(HttpContext http, string username)
    {
        var cache = http.RequestServices.GetRequiredService<IMemoryCache>();
        return await cache.GetOrCreateAsync(CacheKey(username), async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(http.RequestServices.GetRequiredService<AuthOptions>().SessionStampCacheSeconds);
            var db = http.RequestServices.GetRequiredService<DenialsDbContext>();
            var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Username == username, http.RequestAborted);
            return user is null ? "" : For(user);
        }) ?? "";
    }
}
