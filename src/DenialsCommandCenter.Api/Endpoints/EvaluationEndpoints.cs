using DenialsCommandCenter.Api.Analysis;
using DenialsCommandCenter.Api.Auth;
using DenialsCommandCenter.Api.Configuration;
using DenialsCommandCenter.Api.Data;
using DenialsCommandCenter.Domain.Reference;
using DenialsCommandCenter.Domain.Analysis;
using Microsoft.EntityFrameworkCore;

namespace DenialsCommandCenter.Api.Endpoints;

public static class EvaluationEndpoints
{
    public static void MapEvaluationEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/evaluation", async (DenialsDbContext db, ReferenceData reference) =>
            Results.Ok(LabelEvaluator.Evaluate(reference.Labels, await RulePredictionsAsync(db)))).RequireAuthorization(Policies.Manager);

        app.MapPost("/api/evaluation/ai", async (DenialsDbContext db, ReferenceData reference, GeminiOptions gemini, AppealDraftService drafts, CancellationToken ct) =>
        {
            if (!gemini.IsConfigured)
                return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Gemini__ApiKey is not set, so the AI cannot be evaluated.");

            var labels = reference.Labels;
            var rulePredictions = await RulePredictionsAsync(db);
            var aiPredictions = new Dictionary<string, DenialPrediction>();
            var unavailable = 0;
            var consecutiveUnavailable = 0;
            var stoppedEarly = false;
            foreach (var label in labels)
            {
                if (consecutiveUnavailable >= gemini.EvaluationStopAfterUnavailable)
                {
                    stoppedEarly = true;
                    unavailable++;
                    continue;
                }
                var draft = await drafts.ClassifyBlindAsync(label.ClaimId, ct);
                consecutiveUnavailable = draft?.Status == DraftStatuses.Unavailable ? consecutiveUnavailable + 1 : 0;
                if (draft?.AiPrediction is { } prediction) aiPredictions[label.ClaimId] = prediction;
                else unavailable++;
            }
            var agreement = aiPredictions.Count(p => rulePredictions.TryGetValue(p.Key, out var rule) && rule.RootCause == p.Value.RootCause);

            return Results.Ok(new
            {
                rules = LabelEvaluator.Evaluate(labels, rulePredictions),
                ai = LabelEvaluator.Evaluate(labels, aiPredictions),
                aiUnavailable = unavailable,
                stoppedEarly,
                agreementWithRules = agreement,
            });
        }).RequireAuthorization(Policies.Manager).RequireRateLimiting(RateLimits.Expensive);
    }

    private static async Task<Dictionary<string, DenialPrediction>> RulePredictionsAsync(DenialsDbContext db) =>
        await db.DenialAnalyses.AsNoTracking()
            .ToDictionaryAsync(a => a.ClaimId, a => new DenialPrediction(a.RootCause, a.OwningTeam, a.Preventable));
}
