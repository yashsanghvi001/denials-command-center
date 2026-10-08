using System.Security.Claims;

namespace DenialsCommandCenter.Api.Auth;

public static class UserClaims
{
    public const string DisplayName = "display_name";

    public static string Username(this ClaimsPrincipal user) => user.Identity?.Name ?? "";

    public static bool IsManager(this ClaimsPrincipal user) => user.IsInRole(Roles.Manager);
}
