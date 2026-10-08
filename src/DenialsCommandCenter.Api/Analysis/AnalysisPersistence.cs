using System.Text.Json;
using DenialsCommandCenter.Api.Data;
using DenialsCommandCenter.Domain;
using DenialsCommandCenter.Domain.Analysis;

namespace DenialsCommandCenter.Api.Analysis;

public static class AnalysisPersistence
{
    public static void AddAll(DenialsDbContext db, IEnumerable<DenialAnalysis> analyses) => db.DenialAnalyses.AddRange(analyses.Select(ToRow));

    public static DenialAnalysisRow ToRow(DenialAnalysis analysis)
    {
        var classification = analysis.Classification;
        var recoverability = analysis.Recoverability;
        return new DenialAnalysisRow
        {
            ClaimId = analysis.ClaimId,
            PayerId = analysis.PayerId,
            Status = analysis.Status.ToString(),
            DeniedAmount = analysis.DeniedAmount,
            DenialDate = analysis.DenialDate,
            ReasonCodes = string.Join(',', classification.ReasonCodes),
            RootCause = classification.RootCause,
            OwningTeam = classification.OwningTeam,
            Preventable = classification.Preventable,
            Action = classification.Action.ToString(),
            NextAction = classification.NextAction,
            CitationsJson = JsonSerializer.Serialize(classification.Citations, JsonDefaults.Options),
            Confidence = classification.Confidence.ToString(),
            ConfidenceReason = classification.ConfidenceReason,
            Bucket = recoverability.Bucket.ToString(),
            Deadline = recoverability.Deadline,
            DaysToDeadline = recoverability.DaysToDeadline,
            ExpectedValue = recoverability.ExpectedValue,
            PriorityScore = recoverability.PriorityScore,
        };
    }

    public static DenialClassification ToClassification(DenialAnalysisRow row) => new(
        row.RootCause,
        row.OwningTeam,
        row.Preventable,
        Enum.Parse<NextActionType>(row.Action),
        row.NextAction,
        JsonSerializer.Deserialize<List<string>>(row.CitationsJson, JsonDefaults.Options) ?? [],
        Enum.Parse<Confidence>(row.Confidence),
        row.ConfidenceReason,
        row.ReasonCodes.Split(',', StringSplitOptions.RemoveEmptyEntries));
}
