namespace DenialsCommandCenter.Domain.Ingestion;

public sealed record IngestionIssue(
    string Kind, string Severity, string Source, string Reference, string Reason, string? ClaimId = null, decimal? Amount = null)
{
    public string Key => Hashing.Sha256Hex($"{Kind}|{Source}|{Reference}");
}

public static class Severity
{
    public const string Error = "Error";
    public const string Warning = "Warning";
    public const string Info = "Info";
}

public static class IssueKinds
{
    public const string DuplicateFile = nameof(DuplicateFile);
    public const string UnparseableFile = nameof(UnparseableFile);
    public const string DuplicateRemittanceFile = nameof(DuplicateRemittanceFile);
    public const string PartialDuplicateTransaction = nameof(PartialDuplicateTransaction);
    public const string TransactionOutOfBalance = nameof(TransactionOutOfBalance);
    public const string ClaimOutOfBalance = nameof(ClaimOutOfBalance);
    public const string UnmatchedRemittance = nameof(UnmatchedRemittance);
    public const string MemberMismatch = nameof(MemberMismatch);
    public const string UnknownClaimStatus = nameof(UnknownClaimStatus);
    public const string PossibleOverpayment = nameof(PossibleOverpayment);
    public const string UnmatchedReversal = nameof(UnmatchedReversal);
    public const string ClaimsRowInvalid = nameof(ClaimsRowInvalid);
    public const string ClaimsHeaderConflict = nameof(ClaimsHeaderConflict);
    public const string ClaimsDuplicateLine = nameof(ClaimsDuplicateLine);
    public const string WorklogUnmatchedClaim = nameof(WorklogUnmatchedClaim);
    public const string WorklogDuplicateRow = nameof(WorklogDuplicateRow);
    public const string WorklogAmbiguousDate = nameof(WorklogAmbiguousDate);
    public const string WorklogUnparseableDate = nameof(WorklogUnparseableDate);
    public const string WorklogSuspiciousText = nameof(WorklogSuspiciousText);
    public const string WorklogClosedButStillDenied = nameof(WorklogClosedButStillDenied);
    public const string WorklogOpenButPaid = nameof(WorklogOpenButPaid);
    public const string WorklogAmountMismatch = nameof(WorklogAmountMismatch);
}
