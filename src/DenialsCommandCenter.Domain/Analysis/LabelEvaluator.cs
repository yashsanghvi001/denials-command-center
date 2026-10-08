using DenialsCommandCenter.Domain.Reference;

namespace DenialsCommandCenter.Domain.Analysis;

public sealed record DenialPrediction(string RootCause, string OwningTeam, string Preventable);

public sealed record LabelMismatch(string ClaimId, string Field, string Expected, string Predicted);

public sealed record EvaluationReport(
    int Labeled, int Evaluated, int RootCauseCorrect, int OwningTeamCorrect, int PreventableCorrect, int FullyCorrect,
    IReadOnlyList<LabelMismatch> Mismatches, IReadOnlyList<string> Unevaluated);

public static class LabelEvaluator
{
    public static EvaluationReport Evaluate(IReadOnlyList<LabeledDenial> labels, IReadOnlyDictionary<string, DenialPrediction> predictions)
    {
        var mismatches = new List<LabelMismatch>();
        var unevaluated = new List<string>();
        int rootCauseCorrect = 0, owningTeamCorrect = 0, preventableCorrect = 0, fullyCorrect = 0;

        foreach (var label in labels)
        {
            if (!predictions.TryGetValue(label.ClaimId, out var prediction))
            {
                unevaluated.Add(label.ClaimId);
                continue;
            }
            var rootCauseMatches = Compare(label.ClaimId, "root_cause_category", label.RootCause, prediction.RootCause, mismatches);
            var owningTeamMatches = Compare(label.ClaimId, "owning_team", label.OwningTeam, prediction.OwningTeam, mismatches);
            var preventableMatches = Compare(label.ClaimId, "preventable_at_prebill", label.Preventable, prediction.Preventable, mismatches);
            if (rootCauseMatches) rootCauseCorrect++;
            if (owningTeamMatches) owningTeamCorrect++;
            if (preventableMatches) preventableCorrect++;
            if (rootCauseMatches && owningTeamMatches && preventableMatches) fullyCorrect++;
        }

        return new EvaluationReport(labels.Count, labels.Count - unevaluated.Count, rootCauseCorrect, owningTeamCorrect,
            preventableCorrect, fullyCorrect, mismatches, unevaluated);
    }

    private static bool Compare(string claimId, string field, string expected, string predicted, List<LabelMismatch> mismatches)
    {
        var matches = string.Equals(expected.Trim(), predicted.Trim(), StringComparison.OrdinalIgnoreCase);
        if (!matches) mismatches.Add(new LabelMismatch(claimId, field, expected, predicted));
        return matches;
    }
}
