using DenialsCommandCenter.Api.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DenialsCommandCenter.Api.Auth;

public static class UserSeeder
{
    private static readonly (string Username, string DisplayName, string Role)[] Team =
    [
        ("manager", "Practice Manager", Roles.Manager),
        ("karan", "Karan", Roles.Specialist),
        ("priya", "Priya", Roles.Specialist),
        ("anjali", "Anjali", Roles.Specialist),
        ("rahul", "Rahul", Roles.Specialist),
    ];

    public static async Task SeedAsync(DenialsDbContext db, string password, CancellationToken ct = default)
    {
        if (await db.Users.AnyAsync(ct)) return;

        var hasher = new PasswordHasher<UserRow>();
        foreach (var (username, displayName, role) in Team)
        {
            var user = new UserRow { Username = username, DisplayName = displayName, Role = role, PasswordHash = "" };
            user.PasswordHash = hasher.HashPassword(user, password);
            db.Users.Add(user);
        }
        await db.SaveChangesAsync(ct);
    }

    // Email addresses come from configuration (Auth:Emails:{username}) so they can be set or changed without reseeding.
    public static async Task SyncEmailsAsync(DenialsDbContext db, IReadOnlyDictionary<string, string> emails, CancellationToken ct = default)
    {
        var configured = emails
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Value))
            .ToDictionary(entry => entry.Key.ToLowerInvariant(), entry => entry.Value.Trim());
        if (configured.Count == 0) return;

        foreach (var user in await db.Users.Where(u => configured.Keys.Contains(u.Username)).ToListAsync(ct))
            user.Email = configured[user.Username];
        await db.SaveChangesAsync(ct);
    }
}
