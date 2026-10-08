using System.Text.Json;
using DenialsCommandCenter.Api.Data;
using DenialsCommandCenter.Domain;
using DenialsCommandCenter.Domain.Ingestion;

namespace DenialsCommandCenter.Api.Ingestion;

public static class PipelinePersistence
{
    private static string Json<T>(T value) => JsonSerializer.Serialize(value, JsonDefaults.Options);

    public static void AddAll(DenialsDbContext db, PipelineResult result)
    {
        db.Claims.AddRange(result.Claims.Select(c => new ClaimRow
        {
            ClaimId = c.ClaimId, PatientFirst = c.PatientFirst, PatientLast = c.PatientLast, PatientDob = c.PatientDob,
            MemberId = c.MemberId, Payer = c.Payer, PayerId = c.PayerId, DateOfService = c.DateOfService, SubmittedDate = c.SubmittedDate,
            RenderingNpi = c.RenderingNpi, RenderingProvider = c.RenderingProvider, Facility = c.Facility, PlaceOfService = c.PlaceOfService,
            CoderId = c.CoderId, PrebillReviewed = c.PrebillReviewed, TotalCharge = c.TotalCharge, LinesJson = Json(c.Lines),
        }));
        db.RemitEvents.AddRange(result.Events.Select(e => new RemitEventRow
        {
            EventKey = e.EventKey, ClaimId = e.ClaimId, RawClaimRef = e.Payment.RawClaimRef, PayerId = e.PayerId, PayerName = e.PayerName,
            TraceNumber = e.TraceNumber, PaymentDate = e.PaymentDate, SourceFile = e.SourceFile, FileOrder = e.FileOrder,
            Sequence = e.Payment.Sequence, StatusCode = e.Payment.StatusCode, Charge = e.Payment.Charge, Paid = e.Payment.Paid,
            PaymentJson = Json(e.Payment),
        }));
        db.ClaimStates.AddRange(result.States.Select(s => new ClaimStateRow
        {
            ClaimId = s.ClaimId, Status = s.Status.ToString(), BilledCharge = s.BilledCharge, NetPaid = s.NetPaid,
            DeniedAmount = s.DeniedAmount, DenialDate = s.DenialDate, LastRemitDate = s.LastRemitDate,
            DenialLinesJson = Json(s.DenialLines), WasRecouped = s.WasRecouped, PossibleOverpayment = s.PossibleOverpayment, EventCount = s.EventCount,
        }));
        db.WorklogEntries.AddRange(result.Worklog.Select(w => new WorklogEntryRow
        {
            RowNumber = w.RowNumber, ClaimId = w.ClaimId, LoggedDate = w.LoggedDate, LoggedDateCandidates = Json(w.LoggedDateCandidates),
            Patient = w.Patient, Payer = w.Payer, Amount = w.Amount, Notes = w.Notes, Owner = w.Owner, Status = w.Status.ToString(),
            StatusRaw = w.StatusRaw, SuspiciousText = w.SuspiciousText,
        }));
        db.IngestionIssues.AddRange(result.Issues.Select(i => new IngestionIssueRow
        {
            Key = i.Key, Kind = i.Kind, Severity = i.Severity, Source = i.Source, Reference = i.Reference, Reason = i.Reason,
            ClaimId = i.ClaimId, Amount = i.Amount,
        }));
        db.SourceFiles.AddRange(result.Reconciliation.Files.Select(f => new SourceFileRow
        {
            FileName = f.FileName, FileOrder = f.FileOrder, Sha256 = f.Sha256, InterchangeControlNumber = f.InterchangeControlNumber,
            InterchangeDate = f.InterchangeDate, ClaimPayments = f.ClaimPayments, NewEvents = f.NewEvents, DuplicateEvents = f.DuplicateEvents,
            PaymentTotal = f.PaymentTotal, NewPaymentTotal = f.NewPaymentTotal, Outcome = f.Outcome,
        }));
    }
}
