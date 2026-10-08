using DenialsCommandCenter.Domain.Claims;
using DenialsCommandCenter.Domain.Worklog;

namespace DenialsCommandCenter.Domain.Ingestion;

public sealed record RemitSource(string FileName, byte[] Content);

public sealed record PipelineInput(string ClaimsCsv, IReadOnlyList<RemitSource> Remits, IReadOnlyList<WorklogRawRow> WorklogRows, DateOnly Today);

public sealed record SourceFileSummary(
    int FileOrder, string FileName, string Sha256, string? InterchangeControlNumber, DateOnly? InterchangeDate,
    int ClaimPayments, int NewEvents, int DuplicateEvents, decimal PaymentTotal, decimal NewPaymentTotal, string Outcome);

public sealed record ReconciliationReport(
    decimal PayerFilesTotalIncludingDuplicates, decimal DuplicatePaymentsExcluded, decimal PayerFilesTotal,
    decimal PaidToKnownClaims, decimal PaidToUnmatched, decimal ProviderLevelAdjustments, decimal Difference,
    IReadOnlyDictionary<string, int> ClaimsByStatus, IReadOnlyList<SourceFileSummary> Files);

public sealed record PipelineResult(
    IReadOnlyList<ClaimRecord> Claims, IReadOnlyList<RemitEvent> Events, IReadOnlyList<ClaimState> States,
    IReadOnlyList<WorklogEntry> Worklog, IReadOnlyList<IngestionIssue> Issues, ReconciliationReport Reconciliation);
