using DenialsCommandCenter.Domain.Claims;
using DenialsCommandCenter.Domain.Ingestion;
using DenialsCommandCenter.Domain.Reference;

namespace DenialsCommandCenter.Domain.Analysis;

public sealed record DenialAnalysis(
    string ClaimId, string PayerId, ClaimStatus Status, decimal DeniedAmount, DateOnly? DenialDate,
    DenialClassification Classification, Recoverability Recoverability);

public static class DenialAnalyzer
{
    public static IReadOnlyList<DenialAnalysis> Analyze(PipelineResult result, IReadOnlyDictionary<string, PayerRule> payerRules, DateOnly today)
    {
        var ratiosByPayer = AllowedRatios.ByPayer(result.Events);
        var overallRatio = AllowedRatios.Overall(result.Events);
        var claimsById = result.Claims.ToDictionary(c => c.ClaimId);
        var claimsByPatientAndDay = result.Claims.ToLookup(c => (c.MemberId, c.DateOfService));

        return result.States
            .Where(s => s.Status is ClaimStatus.Denied or ClaimStatus.PartiallyDenied)
            .OrderBy(s => s.ClaimId, StringComparer.Ordinal)
            .Select(state =>
            {
                var claim = claimsById[state.ClaimId];
                var payerRule = payerRules.GetValueOrDefault(claim.PayerId);
                var sameDayClaims = claimsByPatientAndDay[(claim.MemberId, claim.DateOfService)].ToList();
                var classification = DenialClassifier.Classify(new DenialContext(claim, state, payerRule, sameDayClaims, today));
                if (payerRule is null && classification.Confidence == Confidence.High)
                    classification = classification with
                    {
                        Confidence = Confidence.Low,
                        ConfidenceReason = $"No filing or appeal windows are configured for payer {claim.PayerId}.",
                    };
                var recoverability = RecoverabilityAssessor.Assess(
                    state, claim.DateOfService, classification, payerRule, ratiosByPayer.GetValueOrDefault(claim.PayerId, overallRatio), today);
                return new DenialAnalysis(claim.ClaimId, claim.PayerId, state.Status, state.DeniedAmount, state.DenialDate, classification, recoverability);
            })
            .ToList();
    }
}
