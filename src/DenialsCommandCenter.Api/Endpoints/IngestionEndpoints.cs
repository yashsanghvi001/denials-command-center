using System.Globalization;
using System.Security.Claims;
using DenialsCommandCenter.Api.Auth;
using DenialsCommandCenter.Api.Data;
using DenialsCommandCenter.Api.Ingestion;
using DenialsCommandCenter.Api.Worklist;

namespace DenialsCommandCenter.Api.Endpoints;

public static class IngestionEndpoints
{
    public static void MapIngestionEndpoints(this IEndpointRouteBuilder app) =>
        app.MapPost("/api/ingestion/run", async (IngestionService service, DenialsDbContext db, ClaimsPrincipal user, bool? force, CancellationToken ct) =>
        {
            IngestionRunSummary summary;
            try
            {
                summary = await service.RunAsync(force ?? false, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                return Results.Problem(title: "Ingestion failed; the last applied data is still served.", detail: ex.Message);
            }
            // A re-run can replace every claim, so record who triggered it and what it produced.
            AuditLog.Record(db, user.Username(), AuditEntities.Ingestion, summary.RunId.ToString(CultureInfo.InvariantCulture), "Run", null, summary);
            await db.SaveChangesAsync(ct);
            return Results.Ok(summary);
        }).RequireAuthorization(Policies.Manager).RequireRateLimiting(RateLimits.Expensive);
}
