using System.Text.Json;
using DenialsCommandCenter.Api.Auth;
using DenialsCommandCenter.Api.Data;
using DenialsCommandCenter.Domain.Claims;
using Microsoft.EntityFrameworkCore;

namespace DenialsCommandCenter.Api.Endpoints;

public static class ClaimEndpoints
{
    public static void MapClaimEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/claims/{claimId}", async (string claimId, DenialsDbContext db) =>
        {
            var claim = await db.Claims.AsNoTracking().SingleOrDefaultAsync(c => c.ClaimId == claimId);
            if (claim is null) return Results.NotFound();

            var state = await db.ClaimStates.AsNoTracking().SingleAsync(s => s.ClaimId == claimId);
            // Same order as ClaimStateProjector.Order: date, reversals first, file order, segment order.
            var events = await db.RemitEvents.AsNoTracking()
                .Where(e => e.ClaimId == claimId)
                .OrderBy(e => e.PaymentDate).ThenBy(e => e.StatusCode == ClaimStateProjector.ReversalStatus ? 0 : 1).ThenBy(e => e.FileOrder).ThenBy(e => e.Sequence)
                .ToListAsync();
            var worklog = await db.WorklogEntries.AsNoTracking().Where(w => w.ClaimId == claimId).OrderBy(w => w.RowNumber).ToListAsync();
            var issues = await db.IngestionIssues.AsNoTracking().Where(i => i.ClaimId == claimId).OrderBy(i => i.Kind).ToListAsync();

            return Results.Ok(new
            {
                claim = new
                {
                    claim.ClaimId, claim.PatientFirst, claim.PatientLast, claim.PatientDob, claim.MemberId, claim.Payer, claim.PayerId,
                    claim.DateOfService, claim.SubmittedDate, claim.RenderingNpi, claim.RenderingProvider, claim.Facility,
                    claim.PlaceOfService, claim.CoderId, claim.PrebillReviewed, claim.TotalCharge,
                },
                lines = JsonSerializer.Deserialize<JsonElement>(claim.LinesJson),
                state = new
                {
                    state.Status, state.BilledCharge, state.NetPaid, state.DeniedAmount, state.DenialDate, state.LastRemitDate,
                    state.WasRecouped, state.PossibleOverpayment, state.EventCount,
                },
                denialLines = JsonSerializer.Deserialize<JsonElement>(state.DenialLinesJson),
                events = events.Select(e => new
                {
                    e.PaymentDate, e.StatusCode, e.Charge, e.Paid, e.PayerName, e.TraceNumber, e.SourceFile,
                    payment = JsonSerializer.Deserialize<JsonElement>(e.PaymentJson),
                }),
                worklog,
                issues,
            });
        }).RequireClaimAccess();
    }
}
