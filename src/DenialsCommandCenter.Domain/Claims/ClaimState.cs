using DenialsCommandCenter.Domain.X12;

namespace DenialsCommandCenter.Domain.Claims;

public sealed record RemitEvent(
    string EventKey, string? ClaimId, string PayerId, string PayerName, string TraceNumber, DateOnly PaymentDate,
    string SourceFile, int FileOrder, ClaimPayment Payment);

public enum ClaimStatus { NoResponse, Paid, PartiallyDenied, Denied, Reversed, Unknown }

public sealed record DenialLine(string ProcedureCode, string Group, string Reason, decimal Amount, IReadOnlyList<string> Remarks);

public sealed record ClaimState(
    string ClaimId, ClaimStatus Status, decimal BilledCharge, decimal NetPaid, decimal DeniedAmount,
    DateOnly? DenialDate, DateOnly? LastRemitDate, IReadOnlyList<DenialLine> DenialLines,
    bool WasRecouped, decimal PossibleOverpayment, int EventCount, int UnmatchedReversals);
