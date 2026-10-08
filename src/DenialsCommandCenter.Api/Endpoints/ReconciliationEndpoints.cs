using DenialsCommandCenter.Api.Auth;
using DenialsCommandCenter.Api.Configuration;
using DenialsCommandCenter.Api.Data;
using DenialsCommandCenter.Api.Ingestion;
using DenialsCommandCenter.Domain.Ingestion;
using Microsoft.EntityFrameworkCore;

namespace DenialsCommandCenter.Api.Endpoints;

public sealed record ExceptionsPage(IReadOnlyList<IngestionIssueRow> Items, int Page, int PageSize, int TotalItems, IReadOnlyList<string> Kinds)
    : PagedResult<IngestionIssueRow>(Items, Page, PageSize, TotalItems);

public static class ReconciliationEndpoints
{
    private static readonly string[] ExceptionSortColumns = ["severity", "kind", "source", "reference", "claimId", "amount"];

    public static void MapReconciliationEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/reconciliation", async (DenialsDbContext db) =>
        {
            var run = await db.IngestionRuns
                .Where(r => r.Outcome == IngestionOutcomes.Applied)
                .OrderByDescending(r => r.Id)
                .FirstOrDefaultAsync();
            return run?.ReportJson is null ? Results.NotFound() : Results.Content(run.ReportJson, "application/json");
        }).RequireAuthorization(Policies.Manager);

        app.MapGet("/api/exceptions", async (DenialsDbContext db, string? kind, string? severity, [AsParameters] GridQuery grid, LimitsOptions limits) =>
        {
            if (grid.Problem(ExceptionSortColumns, limits) is { } problem)
                return ApiResults.BadRequest(problem);
            if (!string.IsNullOrWhiteSpace(severity) && severity is not (Severity.Error or Severity.Warning or Severity.Info))
                return ApiResults.BadRequest($"Severity must be {Severity.Error}, {Severity.Warning} or {Severity.Info}.");

            var query = db.IngestionIssues.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(severity)) query = query.Where(i => i.Severity == severity);
            var kinds = await query.Select(i => i.Kind).Distinct().OrderBy(k => k).ToListAsync();

            if (!string.IsNullOrWhiteSpace(kind)) query = query.Where(i => i.Kind == kind);
            if (grid.SearchPattern is { } pattern)
                query = query.Where(i => EF.Functions.ILike(i.Kind, pattern) || EF.Functions.ILike(i.Source, pattern)
                    || EF.Functions.ILike(i.Reference, pattern) || EF.Functions.ILike(i.Reason, pattern) || EF.Functions.ILike(i.ClaimId!, pattern));

            var totalItems = await query.CountAsync();
            var sorted = grid.Sort switch
            {
                "kind" => query.SortBy(i => i.Kind, grid.Descending),
                "source" => query.SortBy(i => i.Source, grid.Descending),
                "reference" => query.SortBy(i => i.Reference, grid.Descending),
                "claimId" => query.SortBy(i => i.ClaimId, grid.Descending),
                "amount" => query.SortBy(i => i.Amount, grid.Descending),
                _ => query.SortBy(i => i.Severity == "Error" ? 0 : i.Severity == "Warning" ? 1 : 2, grid.Descending),
            };

            var issues = await sorted.ThenBy(i => i.Kind).ThenBy(i => i.Reference).ThenBy(i => i.Key)
                .Skip(grid.Skip(limits)).Take(grid.PageSizeOrDefault(limits))
                .ToListAsync();
            return Results.Ok(new ExceptionsPage(issues, grid.Page, grid.PageSizeOrDefault(limits), totalItems, kinds));
        }).RequireAuthorization(Policies.Manager);
    }
}
