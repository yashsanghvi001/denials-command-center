using System.Security.Claims;
using DenialsCommandCenter.Api.Auth;
using DenialsCommandCenter.Api.Configuration;
using DenialsCommandCenter.Api.Data;
using DenialsCommandCenter.Api.Worklist;
using DenialsCommandCenter.Domain.Analysis;
using Microsoft.EntityFrameworkCore;

namespace DenialsCommandCenter.Api.Endpoints;

public sealed record WorklistRow(
    string ClaimId, string PatientName, string Payer, decimal DeniedAmount, string Bucket, DateOnly? Deadline, int? DaysToDeadline,
    decimal ExpectedValue, decimal PriorityScore, string RootCause, string OwningTeam, string Action, string Confidence,
    string? AssignedTo, string Status);

public sealed record WorklistPage(IReadOnlyList<WorklistRow> Items, int Page, int PageSize, int TotalItems, decimal TotalDenied)
    : PagedResult<WorklistRow>(Items, Page, PageSize, TotalItems);

public sealed record WorkItemResponse(string ClaimId, string? AssignedTo, string Status, DateTimeOffset UpdatedAt, string UpdatedBy);

public sealed record WorkNoteResponse(int Id, string Author, string Text, DateTimeOffset CreatedAt);

public sealed record AuditEntryResponse(long Id, DateTimeOffset At, string Actor, string Entity, string Action, string? BeforeJson, string? AfterJson);

public sealed record StatusChange(string? Status);

public sealed record NoteRequest(string? Text);

public sealed record AssigneeChange(string? Username);

public sealed record SpecialistResponse(string Username, string DisplayName);

public static class WorklistEndpoints
{
    private const string Unassigned = "unassigned";
    private const string AllStatuses = "all";
    private static readonly string[] SortColumns =
        ["claimId", "patientName", "payer", "deniedAmount", "bucket", "daysToDeadline", "expectedValue", "priorityScore", "rootCause", "assignedTo", "status"];

    public static void MapWorklistEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/worklist", async (ClaimsPrincipal user, DenialsDbContext db, string? status, string? assignee, [AsParameters] GridQuery grid, LimitsOptions limits) =>
        {
            var assigneeFilter = assignee?.Trim().ToLowerInvariant();
            if (!string.IsNullOrEmpty(status) && status != AllStatuses && !WorkStatuses.All.Contains(status))
                return ApiResults.BadRequest($"Status must be empty, {AllStatuses}, or one of {string.Join(", ", WorkStatuses.All)}.");
            if (grid.Problem(SortColumns, limits) is { } problem)
                return ApiResults.BadRequest(problem);

            var query = from item in db.WorkItems
                        join analysis in db.DenialAnalyses on item.ClaimId equals analysis.ClaimId
                        join claim in db.Claims on item.ClaimId equals claim.ClaimId
                        select new
                        {
                            item.ClaimId,
                            PatientName = claim.PatientFirst + " " + claim.PatientLast,
                            PatientSortName = claim.PatientLast + ", " + claim.PatientFirst,
                            claim.Payer,
                            analysis.DeniedAmount,
                            analysis.Bucket,
                            BucketRank = analysis.Bucket == nameof(RecoveryBucket.Recoverable) ? 0
                                : analysis.Bucket == nameof(RecoveryBucket.MaybeEligibility) ? 1
                                : analysis.Bucket == nameof(RecoveryBucket.LostWindowExpired) ? 2
                                : 3,
                            analysis.Deadline,
                            analysis.DaysToDeadline,
                            analysis.ExpectedValue,
                            analysis.PriorityScore,
                            analysis.RootCause,
                            analysis.OwningTeam,
                            analysis.Action,
                            analysis.Confidence,
                            item.AssignedTo,
                            item.Status,
                        };

            if (!user.IsManager())
            {
                var username = user.Username();
                query = query.Where(x => x.AssignedTo == username);
            }
            else if (assigneeFilter == Unassigned)
                query = query.Where(x => x.AssignedTo == null);
            else if (!string.IsNullOrEmpty(assigneeFilter))
                query = query.Where(x => x.AssignedTo == assigneeFilter);

            if (string.IsNullOrWhiteSpace(status))
                query = query.Where(x => x.Status != WorkStatuses.Closed);
            else if (status != AllStatuses)
                query = query.Where(x => x.Status == status);

            if (grid.SearchPattern is { } pattern)
                query = query.Where(x => EF.Functions.ILike(x.ClaimId, pattern) || EF.Functions.ILike(x.PatientName, pattern) || EF.Functions.ILike(x.PatientSortName, pattern)
                    || EF.Functions.ILike(x.Payer, pattern) || EF.Functions.ILike(x.RootCause, pattern));

            var totalItems = await query.CountAsync();
            var totalDenied = await query.SumAsync(x => x.DeniedAmount);

            var sorted = grid.Sort switch
            {
                "claimId" => query.SortBy(x => x.ClaimId, grid.Descending),
                "patientName" => query.SortBy(x => x.PatientSortName, grid.Descending),
                "payer" => query.SortBy(x => x.Payer, grid.Descending),
                "deniedAmount" => query.SortBy(x => x.DeniedAmount, grid.Descending),
                "bucket" => query.SortBy(x => x.BucketRank, grid.Descending),
                "daysToDeadline" => query.SortBy(x => x.DaysToDeadline, grid.Descending),
                "expectedValue" => query.SortBy(x => x.ExpectedValue, grid.Descending),
                "priorityScore" => query.SortBy(x => x.PriorityScore, grid.Descending),
                "rootCause" => query.SortBy(x => x.RootCause, grid.Descending),
                "assignedTo" => query.SortBy(x => x.AssignedTo, grid.Descending),
                "status" => query.SortBy(x => x.Status, grid.Descending),
                _ => query.OrderBy(x => x.BucketRank)
                    .ThenByDescending(x => x.PriorityScore)
                    .ThenBy(x => x.DaysToDeadline ?? int.MaxValue)
                    .ThenByDescending(x => x.DeniedAmount),
            };

            var rows = await sorted.ThenBy(x => x.ClaimId)
                .Skip(grid.Skip(limits)).Take(grid.PageSizeOrDefault(limits))
                .Select(x => new WorklistRow(
                    x.ClaimId, x.PatientName, x.Payer, x.DeniedAmount, x.Bucket, x.Deadline, x.DaysToDeadline, x.ExpectedValue,
                    x.PriorityScore, x.RootCause, x.OwningTeam, x.Action, x.Confidence, x.AssignedTo, x.Status))
                .ToListAsync();
            return Results.Ok(new WorklistPage(rows, grid.Page, grid.PageSizeOrDefault(limits), totalItems, totalDenied));
        });

        app.MapGet("/api/worklist/{claimId}", async (string claimId, DenialsDbContext db) =>
        {
            var item = await db.WorkItems.AsNoTracking().SingleOrDefaultAsync(w => w.ClaimId == claimId);
            if (item is null) return Results.NotFound();

            var notes = await db.WorkNotes.AsNoTracking()
                .Where(n => n.ClaimId == claimId).OrderBy(n => n.CreatedAt).ThenBy(n => n.Id)
                .Select(n => new WorkNoteResponse(n.Id, n.Author, n.Text, n.CreatedAt)).ToListAsync();
            var audit = await db.AuditEntries.AsNoTracking()
                .Where(a => a.EntityId == claimId && (a.Entity == AuditEntities.WorkItem || a.Entity == AuditEntities.AppealLetter))
                .OrderBy(a => a.At).ThenBy(a => a.Id)
                .Select(a => new AuditEntryResponse(a.Id, a.At, a.Actor, a.Entity, a.Action, a.BeforeJson, a.AfterJson)).ToListAsync();
            return Results.Ok(new { item = ToResponse(item), notes, audit });
        }).RequireClaimAccess();

        app.MapPatch("/api/worklist/{claimId}/status", async (string claimId, StatusChange change, ClaimsPrincipal user, DenialsDbContext db) =>
        {
            if (change.Status is null || !WorkStatuses.All.Contains(change.Status))
                return ApiResults.BadRequest($"Status must be one of {string.Join(", ", WorkStatuses.All)}.");
            var item = await db.WorkItems.SingleOrDefaultAsync(w => w.ClaimId == claimId);
            if (item is null) return Results.NotFound();

            if (item.Status == change.Status) return Results.Ok(ToResponse(item));

            var before = WorkItemSnapshot.Of(item);
            item.Status = change.Status;
            Touch(item, user);
            AuditLog.Record(db, user.Username(), AuditEntities.WorkItem, claimId, "StatusChanged", before, WorkItemSnapshot.Of(item));
            await db.SaveChangesAsync();
            return Results.Ok(ToResponse(item));
        }).RequireClaimAccess();

        app.MapPost("/api/worklist/{claimId}/notes", async (string claimId, NoteRequest request, ClaimsPrincipal user, DenialsDbContext db, LimitsOptions limits) =>
        {
            var text = request.Text?.Trim() ?? "";
            if (text.Length == 0 || text.Length > limits.MaxNoteLength)
                return ApiResults.BadRequest($"A note must be 1 to {limits.MaxNoteLength} characters.");
            var item = await db.WorkItems.SingleOrDefaultAsync(w => w.ClaimId == claimId);
            if (item is null) return Results.NotFound();

            var note = new WorkNoteRow { ClaimId = claimId, Author = user.Username(), Text = text, CreatedAt = DateTimeOffset.UtcNow };
            db.WorkNotes.Add(note);
            Touch(item, user);
            AuditLog.Record(db, user.Username(), AuditEntities.WorkItem, claimId, "NoteAdded", null, new { text });
            await db.SaveChangesAsync();
            return Results.Created($"/api/worklist/{claimId}", new WorkNoteResponse(note.Id, note.Author, note.Text, note.CreatedAt));
        }).RequireClaimAccess();

        app.MapPut("/api/worklist/{claimId}/assignee", async (string claimId, AssigneeChange change, ClaimsPrincipal user, DenialsDbContext db) =>
        {
            var item = await db.WorkItems.SingleOrDefaultAsync(w => w.ClaimId == claimId);
            if (item is null) return Results.NotFound();
            var username = string.IsNullOrWhiteSpace(change.Username) ? null : change.Username.Trim().ToLowerInvariant();
            if (username is not null && !await db.Users.AnyAsync(u => u.Username == username && u.Role == Roles.Specialist))
                return ApiResults.BadRequest("Work can only be assigned to a denials specialist.");

            if (item.AssignedTo == username) return Results.Ok(ToResponse(item));

            var before = WorkItemSnapshot.Of(item);
            item.AssignedTo = username;
            Touch(item, user);
            AuditLog.Record(db, user.Username(), AuditEntities.WorkItem, claimId, "Reassigned", before, WorkItemSnapshot.Of(item));
            await db.SaveChangesAsync();
            return Results.Ok(ToResponse(item));
        }).RequireAuthorization(Policies.Manager);

        app.MapGet("/api/users", async (DenialsDbContext db) => Results.Ok(await db.Users.AsNoTracking()
            .Where(u => u.Role == Roles.Specialist)
            .OrderBy(u => u.DisplayName)
            .Select(u => new SpecialistResponse(u.Username, u.DisplayName))
            .ToListAsync()))
            .RequireAuthorization(Policies.Manager);
    }

    private static void Touch(WorkItemRow item, ClaimsPrincipal user)
    {
        item.UpdatedAt = DateTimeOffset.UtcNow;
        item.UpdatedBy = user.Username();
    }

    private static WorkItemResponse ToResponse(WorkItemRow item) => new(item.ClaimId, item.AssignedTo, item.Status, item.UpdatedAt, item.UpdatedBy);
}
