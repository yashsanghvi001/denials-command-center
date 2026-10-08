using System.Security.Claims;
using System.Text.Json;
using DenialsCommandCenter.Api.Analysis;
using DenialsCommandCenter.Api.Auth;
using DenialsCommandCenter.Api.Data;
using DenialsCommandCenter.Api.Worklist;
using DenialsCommandCenter.Domain;
using DenialsCommandCenter.Domain.Analysis;
using DenialsCommandCenter.Domain.Appeals;
using Microsoft.EntityFrameworkCore;

namespace DenialsCommandCenter.Api.Endpoints;

public sealed record DenialAnalysisResponse(
    string ClaimId, string PayerId, string Status, decimal DeniedAmount, DateOnly? DenialDate, string ReasonCodes,
    string RootCause, string OwningTeam, string Preventable, string Action, string NextAction, IReadOnlyList<string> Citations,
    string Confidence, string? ConfidenceReason, string Bucket, DateOnly? Deadline, int? DaysToDeadline, decimal ExpectedValue, decimal PriorityScore);

public sealed record DraftResponse(
    string Source, string Status, string? StatusReason, string? Letter, IReadOnlyList<string> Citations, DenialPrediction? AiPrediction, string? AiConfidence);

public static class DenialEndpoints
{
    private static readonly string[] BucketOrder = Enum.GetNames<RecoveryBucket>();

    public static void MapDenialEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/denials/summary", async (DenialsDbContext db) =>
        {
            var analyses = await db.DenialAnalyses.AsNoTracking().ToListAsync();
            var recoverable = analyses.Where(a => a.Bucket == nameof(RecoveryBucket.Recoverable)).ToList();
            return Results.Ok(new
            {
                openDenials = analyses.Count,
                deniedAmount = analyses.Sum(a => a.DeniedAmount),
                expectedValue = analyses.Sum(a => a.ExpectedValue),
                buckets = BucketOrder.Select(bucket =>
                {
                    var inBucket = analyses.Where(a => a.Bucket == bucket).ToList();
                    return new { bucket, claims = inBucket.Count, deniedAmount = inBucket.Sum(a => a.DeniedAmount), expectedValue = inBucket.Sum(a => a.ExpectedValue) };
                }),
                dueWithin14Days = DueWithin(recoverable, 14),
                dueWithin30Days = DueWithin(recoverable, 30),
            });
        }).RequireAuthorization(Policies.Manager);

        app.MapGet("/api/denials", async (DenialsDbContext db, string? bucket, string? payerId, string? rootCause) =>
        {
            if (!string.IsNullOrWhiteSpace(bucket) && !BucketOrder.Contains(bucket))
                return ApiResults.BadRequest($"Bucket must be one of {string.Join(", ", BucketOrder)}.");
            var query = db.DenialAnalyses.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(bucket)) query = query.Where(a => a.Bucket == bucket);
            if (!string.IsNullOrWhiteSpace(payerId)) query = query.Where(a => a.PayerId == payerId);
            if (!string.IsNullOrWhiteSpace(rootCause)) query = query.Where(a => a.RootCause == rootCause);
            var analyses = await query.ToListAsync();
            return Results.Ok(analyses
                .OrderBy(a => Array.IndexOf(BucketOrder, a.Bucket))
                .ThenByDescending(a => a.PriorityScore)
                .ThenBy(a => a.DaysToDeadline ?? int.MaxValue)
                .ThenBy(a => a.ClaimId, StringComparer.Ordinal)
                .Select(ToResponse));
        }).RequireAuthorization(Policies.Manager);

        app.MapGet("/api/denials/{claimId}", async (string claimId, DenialsDbContext db, AppealDraftService drafts, CancellationToken ct) =>
        {
            var analysis = await db.DenialAnalyses.AsNoTracking().SingleOrDefaultAsync(a => a.ClaimId == claimId, ct);
            if (analysis is null) return Results.NotFound();
            var draft = await drafts.FindCurrentLetterAsync(claimId, ct);
            return Results.Ok(new { analysis = ToResponse(analysis), draft = draft is null ? null : await ToResponseAsync(draft, db, ct) });
        }).RequireClaimAccess();

        app.MapPost("/api/denials/{claimId}/draft", async (string claimId, ClaimsPrincipal user, AppealDraftService drafts, DenialsDbContext db, CancellationToken ct) =>
        {
            var draft = await drafts.DraftLetterAsync(claimId, ct);
            if (draft is null) return Results.NotFound();
            AuditLog.Record(db, user.Username(), AuditEntities.AppealLetter, claimId, "LetterDrafted", null, new { draft.Source, draft.Status });
            await db.SaveChangesAsync(ct);
            return Results.Ok(await ToResponseAsync(draft, db, ct));
        }).RequireClaimAccess().RequireRateLimiting(RateLimits.Expensive);

        app.MapGet("/api/review-queue", async (DenialsDbContext db, AppealDraftService drafts, CancellationToken ct) =>
        {
            var analyses = await db.DenialAnalyses.AsNoTracking().ToListAsync(ct);
            var claimIdsWithLetters = await db.AppealDrafts.AsNoTracking()
                .Where(d => d.Purpose == DraftPurposes.Letter)
                .Select(d => d.ClaimId)
                .Distinct()
                .ToListAsync(ct);
            var currentDraftByClaim = new Dictionary<string, AppealDraftRow>();
            foreach (var claimId in claimIdsWithLetters)
                if (await drafts.FindCurrentLetterDraftAsync(claimId, ct) is { } draft)
                    currentDraftByClaim[claimId] = draft;

            return Results.Ok(analyses
                .Select(analysis => new { analysis, reason = ReviewReason(analysis, currentDraftByClaim.GetValueOrDefault(analysis.ClaimId)) })
                .Where(item => item.reason is not null)
                .OrderByDescending(item => item.analysis.PriorityScore)
                .ThenBy(item => item.analysis.ClaimId, StringComparer.Ordinal)
                .Select(item => new { analysis = ToResponse(item.analysis), item.reason }));
        }).RequireAuthorization(Policies.Manager).RequireRateLimiting(RateLimits.Expensive);
    }

    private static string? ReviewReason(DenialAnalysisRow analysis, AppealDraftRow? draft)
    {
        if (analysis.Confidence == nameof(Confidence.Low))
            return analysis.ConfidenceReason ?? "The rules engine is not confident.";
        if (draft?.Status == DraftStatuses.Rejected)
            return $"The AI draft was rejected: {draft.StatusReason}";
        if (draft?.AiRootCause is { } aiRootCause && aiRootCause != analysis.RootCause)
            return $"The AI classified this as '{aiRootCause}' but the rules say '{analysis.RootCause}'.";
        if (draft?.AiOwningTeam is { } aiOwningTeam && aiOwningTeam != analysis.OwningTeam)
            return $"The AI assigned this to '{aiOwningTeam}' but the rules say '{analysis.OwningTeam}'.";
        if (draft?.AiPreventable is { } aiPreventable && aiPreventable != analysis.Preventable)
            return $"The AI marked preventable '{aiPreventable}' but the rules say '{analysis.Preventable}'.";
        if (draft?.AiConfidence == nameof(Confidence.Low))
            return "The AI is not confident in its draft.";
        return null;
    }

    private static object DueWithin(IReadOnlyList<DenialAnalysisRow> recoverable, int days)
    {
        var due = recoverable.Where(a => a.DaysToDeadline <= days).ToList();
        return new { claims = due.Count, deniedAmount = due.Sum(a => a.DeniedAmount) };
    }

    // Placeholders such as the patient name are filled in only here, on the way out, so they never reach the AI.
    private static async Task<DraftResponse> ToResponseAsync(AppealDraft draft, DenialsDbContext db, CancellationToken ct)
    {
        var claim = RowMapping.ToClaimRecord(await db.Claims.AsNoTracking().SingleAsync(c => c.ClaimId == draft.ClaimId, ct));
        var denialDate = await db.DenialAnalyses.Where(a => a.ClaimId == draft.ClaimId).Select(a => a.DenialDate).SingleAsync(ct);
        return new DraftResponse(draft.Source, draft.Status, draft.StatusReason,
            draft.LetterTemplate is null ? null : LetterPlaceholders.Merge(draft.LetterTemplate, claim, denialDate),
            draft.Citations, draft.AiPrediction, draft.AiConfidence);
    }

    private static IReadOnlyList<string> Citations(string json) => JsonSerializer.Deserialize<List<string>>(json, JsonDefaults.Options) ?? [];

    private static DenialAnalysisResponse ToResponse(DenialAnalysisRow row) => new(
        row.ClaimId, row.PayerId, row.Status, row.DeniedAmount, row.DenialDate, row.ReasonCodes, row.RootCause, row.OwningTeam,
        row.Preventable, row.Action, row.NextAction, Citations(row.CitationsJson), row.Confidence, row.ConfidenceReason, row.Bucket,
        row.Deadline, row.DaysToDeadline, row.ExpectedValue, row.PriorityScore);
}
