using DenialsCommandCenter.Api.Auth;
using DenialsCommandCenter.Api.Data;
using DenialsCommandCenter.Domain.Analysis;
using DenialsCommandCenter.Domain.Worklog;
using Microsoft.EntityFrameworkCore;

namespace DenialsCommandCenter.Api.Worklist;

public static class WorkItemSync
{
    private const string SystemActor = "system";

    public static async Task CreateMissingAsync(
        DenialsDbContext db, IReadOnlyList<DenialAnalysis> analyses, IReadOnlyList<WorklogEntry> worklog, CancellationToken ct)
    {
        var existing = await db.WorkItems.Select(w => w.ClaimId).ToHashSetAsync(ct);
        var specialists = await db.Users.Where(u => u.Role == Roles.Specialist).Select(u => u.Username).ToHashSetAsync(ct);
        var loggedByClaim = worklog.Where(w => w.ClaimId is not null).GroupBy(w => w.ClaimId!).ToDictionary(g => g.Key, g => g.OrderBy(w => w.RowNumber).ToList());
        var now = DateTimeOffset.UtcNow;

        foreach (var analysis in analyses.Where(a => !existing.Contains(a.ClaimId)))
        {
            var entries = loggedByClaim.GetValueOrDefault(analysis.ClaimId) ?? [];
            var owner = entries.LastOrDefault(e => e.Owner is not null)?.Owner?.ToLowerInvariant();
            var item = new WorkItemRow
            {
                ClaimId = analysis.ClaimId,
                AssignedTo = owner is not null && specialists.Contains(owner) ? owner : null,
                Status = InitialStatus(entries.LastOrDefault()?.Status),
                UpdatedAt = now,
                UpdatedBy = SystemActor,
            };
            db.WorkItems.Add(item);
            AuditLog.Record(db, SystemActor, AuditEntities.WorkItem, item.ClaimId, "Created", null, WorkItemSnapshot.Of(item));
        }
    }

    // A worklog "closed" on a claim that is still denied means the work is not done, so it starts open.
    private static string InitialStatus(WorklogStatus? logged) => logged switch
    {
        WorklogStatus.InProgress => WorkStatuses.InProgress,
        WorklogStatus.PendingPayer => WorkStatuses.PendingPayer,
        _ => WorkStatuses.Open,
    };
}
