using DenialsCommandCenter.Api.Data;

namespace DenialsCommandCenter.Api.Worklist;

public static class WorkStatuses
{
    public const string Open = nameof(Open);
    public const string InProgress = nameof(InProgress);
    public const string PendingPayer = nameof(PendingPayer);
    public const string Closed = nameof(Closed);

    public static readonly IReadOnlyList<string> All = [Open, InProgress, PendingPayer, Closed];
}

public static class AuditEntities
{
    public const string WorkItem = nameof(WorkItem);
    public const string AppealLetter = nameof(AppealLetter);
    public const string Ingestion = nameof(Ingestion);
}

public sealed record WorkItemSnapshot(string? AssignedTo, string Status)
{
    public static WorkItemSnapshot Of(WorkItemRow item) => new(item.AssignedTo, item.Status);
}
