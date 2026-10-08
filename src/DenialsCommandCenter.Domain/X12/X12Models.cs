namespace DenialsCommandCenter.Domain.X12;

public sealed record Adjustment(string Group, string Reason, decimal Amount);

public sealed record ServicePayment(
    string ProcedureCode, IReadOnlyList<string> Modifiers, decimal Charge, decimal Paid, decimal Units,
    DateOnly? ServiceDate, IReadOnlyList<Adjustment> Adjustments, IReadOnlyList<string> Remarks);

public sealed record ClaimPayment(
    int Sequence, string RawClaimRef, string StatusCode, decimal Charge, decimal Paid, decimal PatientResponsibility,
    string PayerClaimControlNumber, string FrequencyCode, string PatientLastName, string PatientFirstName,
    string MemberId, string RenderingNpi, DateOnly? ReceivedDate,
    IReadOnlyList<Adjustment> Adjustments, IReadOnlyList<string> Remarks, IReadOnlyList<ServicePayment> Services);

public sealed record ProviderAdjustment(string ReasonCode, string Reference, decimal Amount);

public sealed record RemittanceTransaction(
    string PayerName, string PayerId, string TraceNumber, decimal PaymentAmount, DateOnly PaymentDate,
    IReadOnlyList<ClaimPayment> Claims, IReadOnlyList<ProviderAdjustment> ProviderAdjustments);

public sealed record Remittance(string InterchangeControlNumber, DateOnly InterchangeDate, IReadOnlyList<RemittanceTransaction> Transactions);
