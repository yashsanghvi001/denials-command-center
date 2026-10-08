using System.Text.Json;
using DenialsCommandCenter.Domain;
using DenialsCommandCenter.Domain.Claims;

namespace DenialsCommandCenter.Api.Data;

public static class RowMapping
{
    public static ClaimRecord ToClaimRecord(ClaimRow row) => new(
        row.ClaimId, row.PatientFirst, row.PatientLast, row.PatientDob, row.MemberId, row.Payer, row.PayerId,
        row.DateOfService, row.SubmittedDate, row.RenderingNpi, row.RenderingProvider, row.Facility, row.PlaceOfService,
        row.CoderId, row.PrebillReviewed, JsonSerializer.Deserialize<List<ClaimLine>>(row.LinesJson, JsonDefaults.Options) ?? []);

    public static ClaimState ToClaimState(ClaimStateRow row) => new(
        row.ClaimId, Enum.Parse<ClaimStatus>(row.Status), row.BilledCharge, row.NetPaid, row.DeniedAmount, row.DenialDate, row.LastRemitDate,
        JsonSerializer.Deserialize<List<DenialLine>>(row.DenialLinesJson, JsonDefaults.Options) ?? [],
        row.WasRecouped, row.PossibleOverpayment, row.EventCount, UnmatchedReversals: 0);
}
