using System.Text.Json;
using DenialsCommandCenter.Api.Data;
using DenialsCommandCenter.Domain;

namespace DenialsCommandCenter.Api.Worklist;

public static class AuditLog
{
    public static void Record(DenialsDbContext db, string actor, string entity, string entityId, string action, object? before, object? after) =>
        db.AuditEntries.Add(new AuditEntryRow
        {
            At = DateTimeOffset.UtcNow,
            Actor = actor,
            Entity = entity,
            EntityId = entityId,
            Action = action,
            BeforeJson = before is null ? null : JsonSerializer.Serialize(before, JsonDefaults.Options),
            AfterJson = after is null ? null : JsonSerializer.Serialize(after, JsonDefaults.Options),
        });
}
