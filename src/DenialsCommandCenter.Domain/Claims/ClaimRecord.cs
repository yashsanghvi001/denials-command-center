namespace DenialsCommandCenter.Domain.Claims;

public sealed record ClaimLine(int LineNo, string Cpt, string Modifier, decimal Units, decimal Charge, IReadOnlyList<string> DiagnosisCodes, string AuthNumber);

public sealed record ClaimRecord(
    string ClaimId, string PatientFirst, string PatientLast, DateOnly PatientDob, string MemberId, string Payer, string PayerId,
    DateOnly DateOfService, DateOnly SubmittedDate, string RenderingNpi, string RenderingProvider, string Facility,
    string PlaceOfService, string CoderId, bool PrebillReviewed, IReadOnlyList<ClaimLine> Lines)
{
    public decimal TotalCharge => Lines.Sum(l => l.Charge);
}
