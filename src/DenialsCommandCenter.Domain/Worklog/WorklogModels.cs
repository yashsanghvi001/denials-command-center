namespace DenialsCommandCenter.Domain.Worklog;

public sealed record WorklogRawRow(int RowNumber, string Logged, string Claim, string Patient, string Payer, string Amount, string Notes, string Owner, string Status);

public enum WorklogStatus { Open, InProgress, PendingPayer, Closed, Unknown }

public sealed record WorklogEntry(
    int RowNumber, string? ClaimId, DateOnly? LoggedDate, IReadOnlyList<DateOnly> LoggedDateCandidates, string Patient,
    string? Payer, decimal? Amount, string Notes, string? Owner, WorklogStatus Status, string StatusRaw, bool SuspiciousText);
