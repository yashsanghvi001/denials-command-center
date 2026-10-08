namespace DenialsCommandCenter.Api.Data;

// Every table here except Users, WorkItems, WorkNotes, AuditEntries and AppealDrafts is derived: an applied ingestion run truncates and rebuilds them from the source files.
// Users, WorkItems, WorkNotes, AuditEntries and AppealDrafts hold data people entered or AI drafts, and ingestion never touches them.

public sealed class ClaimRow
{
    public required string ClaimId { get; set; }
    public required string PatientFirst { get; set; }
    public required string PatientLast { get; set; }
    public DateOnly PatientDob { get; set; }
    public required string MemberId { get; set; }
    public required string Payer { get; set; }
    public required string PayerId { get; set; }
    public DateOnly DateOfService { get; set; }
    public DateOnly SubmittedDate { get; set; }
    public required string RenderingNpi { get; set; }
    public required string RenderingProvider { get; set; }
    public required string Facility { get; set; }
    public required string PlaceOfService { get; set; }
    public required string CoderId { get; set; }
    public bool PrebillReviewed { get; set; }
    public decimal TotalCharge { get; set; }
    public required string LinesJson { get; set; }
}

public sealed class RemitEventRow
{
    public required string EventKey { get; set; }
    public string? ClaimId { get; set; }
    public required string RawClaimRef { get; set; }
    public required string PayerId { get; set; }
    public required string PayerName { get; set; }
    public required string TraceNumber { get; set; }
    public DateOnly PaymentDate { get; set; }
    public required string SourceFile { get; set; }
    public int FileOrder { get; set; }
    public int Sequence { get; set; }
    public required string StatusCode { get; set; }
    public decimal Charge { get; set; }
    public decimal Paid { get; set; }
    public required string PaymentJson { get; set; }
}

public sealed class ClaimStateRow
{
    public required string ClaimId { get; set; }
    public required string Status { get; set; }
    public decimal BilledCharge { get; set; }
    public decimal NetPaid { get; set; }
    public decimal DeniedAmount { get; set; }
    public DateOnly? DenialDate { get; set; }
    public DateOnly? LastRemitDate { get; set; }
    public required string DenialLinesJson { get; set; }
    public bool WasRecouped { get; set; }
    public decimal PossibleOverpayment { get; set; }
    public int EventCount { get; set; }
}

public sealed class WorklogEntryRow
{
    public int RowNumber { get; set; }
    public string? ClaimId { get; set; }
    public DateOnly? LoggedDate { get; set; }
    public required string LoggedDateCandidates { get; set; }
    public required string Patient { get; set; }
    public string? Payer { get; set; }
    public decimal? Amount { get; set; }
    public required string Notes { get; set; }
    public string? Owner { get; set; }
    public required string Status { get; set; }
    public required string StatusRaw { get; set; }
    public bool SuspiciousText { get; set; }
}

public sealed class IngestionIssueRow
{
    public required string Key { get; set; }
    public required string Kind { get; set; }
    public required string Severity { get; set; }
    public required string Source { get; set; }
    public required string Reference { get; set; }
    public required string Reason { get; set; }
    public string? ClaimId { get; set; }
    public decimal? Amount { get; set; }
}

public sealed class SourceFileRow
{
    public required string FileName { get; set; }
    public int FileOrder { get; set; }
    public required string Sha256 { get; set; }
    public string? InterchangeControlNumber { get; set; }
    public DateOnly? InterchangeDate { get; set; }
    public int ClaimPayments { get; set; }
    public int NewEvents { get; set; }
    public int DuplicateEvents { get; set; }
    public decimal PaymentTotal { get; set; }
    public decimal NewPaymentTotal { get; set; }
    public required string Outcome { get; set; }
}

public sealed class IngestionRun
{
    public int Id { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public required string InputHash { get; set; }
    public required string Outcome { get; set; }
    public int Events { get; set; }
    public int Issues { get; set; }
    public decimal Difference { get; set; }
    public string? ReportJson { get; set; }
}

public sealed class DenialAnalysisRow
{
    public required string ClaimId { get; set; }
    public required string PayerId { get; set; }
    public required string Status { get; set; }
    public decimal DeniedAmount { get; set; }
    public DateOnly? DenialDate { get; set; }
    public required string ReasonCodes { get; set; }
    public required string RootCause { get; set; }
    public required string OwningTeam { get; set; }
    public required string Preventable { get; set; }
    public required string Action { get; set; }
    public required string NextAction { get; set; }
    public required string CitationsJson { get; set; }
    public required string Confidence { get; set; }
    public string? ConfidenceReason { get; set; }
    public required string Bucket { get; set; }
    public DateOnly? Deadline { get; set; }
    public int? DaysToDeadline { get; set; }
    public decimal ExpectedValue { get; set; }
    public decimal PriorityScore { get; set; }
}

public sealed class AppealDraftRow
{
    public required string FactsHash { get; set; }
    public required string ClaimId { get; set; }
    public required string Purpose { get; set; }
    public required string Model { get; set; }
    public required string Status { get; set; }
    public string? StatusReason { get; set; }
    public string? LetterTemplate { get; set; }
    public required string CitationsJson { get; set; }
    public string? AiRootCause { get; set; }
    public string? AiOwningTeam { get; set; }
    public string? AiPreventable { get; set; }
    public string? AiConfidence { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class UserRow
{
    public int Id { get; set; }
    public required string Username { get; set; }
    public required string DisplayName { get; set; }
    public required string Role { get; set; }
    public required string PasswordHash { get; set; }
    public string? Email { get; set; }
    public string? PasswordResetTokenHash { get; set; }
    public DateTimeOffset? PasswordResetExpiresAt { get; set; }
}

public sealed class WorkItemRow
{
    public required string ClaimId { get; set; }
    public string? AssignedTo { get; set; }
    public required string Status { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public required string UpdatedBy { get; set; }

    // Maps to Postgres xmin: a save fails if someone else changed the row after it was read, so a reassignment
    // cannot be silently overwritten and every audit "before" snapshot is the real previous state.
    public uint Version { get; set; }
}

public sealed class WorkNoteRow
{
    public int Id { get; set; }
    public required string ClaimId { get; set; }
    public required string Author { get; set; }
    public required string Text { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class AuditEntryRow
{
    public long Id { get; set; }
    public DateTimeOffset At { get; set; }
    public required string Actor { get; set; }
    public required string Entity { get; set; }
    public required string EntityId { get; set; }
    public required string Action { get; set; }
    public string? BeforeJson { get; set; }
    public string? AfterJson { get; set; }
}
