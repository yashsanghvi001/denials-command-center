using System.Security.Claims;
using DenialsCommandCenter.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace DenialsCommandCenter.Api.Auth;

public static class ClaimAccess
{
    public static async Task<bool> CanAccessAsync(ClaimsPrincipal user, string claimId, DenialsDbContext db, CancellationToken ct = default)
    {
        if (user.IsManager()) return true;
        var username = user.Username();
        return await db.WorkItems.AnyAsync(w => w.ClaimId == claimId && w.AssignedTo == username, ct);
    }

    // Claims carry patient data, so the rule "a specialist sees only claims assigned to them" is declared on the
    // route rather than repeated inside each handler, where a new endpoint could forget it.
    public static RouteHandlerBuilder RequireClaimAccess(this RouteHandlerBuilder builder) =>
        builder.AddEndpointFilter(async (context, next) =>
        {
            var http = context.HttpContext;
            var claimId = http.GetRouteValue("claimId") as string ?? "";
            var db = http.RequestServices.GetRequiredService<DenialsDbContext>();
            return await CanAccessAsync(http.User, claimId, db, http.RequestAborted) ? await next(context) : Results.Forbid();
        });
}
