using DenialsCommandCenter.Api.Analysis;
using DenialsCommandCenter.Api.Ingestion;
using DenialsCommandCenter.Domain.Analysis;
using DenialsCommandCenter.Domain.Ingestion;
using DenialsCommandCenter.Domain.Reference;

namespace DenialsCommandCenter.Tests.Analysis;

// Expected values were computed independently from the same data pack.
public class DenialAnalysisGoldenTests
{
    private static readonly DateOnly Today = new(2026, 9, 30);
    private static readonly Lazy<PipelineResult> Pipeline = new(() => IngestionPipeline.Run(DataPackLoader.Load(TestData.Dir, Today)));
    private static readonly Lazy<ReferenceData> Reference = new(() => ReferenceDataLoader.Load(TestData.Dir));
    private static readonly Lazy<IReadOnlyList<DenialAnalysis>> CachedAnalyses =
        new(() => DenialAnalyzer.Analyze(Pipeline.Value, Reference.Value.PayerRules, Today));
    private static IReadOnlyList<DenialAnalysis> Analyses => CachedAnalyses.Value;

    private static (int Claims, decimal Denied, decimal Expected) Bucket(RecoveryBucket bucket)
    {
        var inBucket = Analyses.Where(a => a.Recoverability.Bucket == bucket).ToList();
        return (inBucket.Count, inBucket.Sum(a => a.DeniedAmount), inBucket.Sum(a => a.Recoverability.ExpectedValue));
    }

    [Fact]
    public void Every_open_denial_is_analyzed() => Assert.Equal(155, Analyses.Count);

    [Fact]
    public void Money_by_bucket()
    {
        Assert.Equal((55, 11650.00m, 7090.50m), Bucket(RecoveryBucket.Recoverable));
        Assert.Equal((14, 2975.00m, 1811.02m), Bucket(RecoveryBucket.MaybeEligibility));
        Assert.Equal((38, 7615.00m, 0m), Bucket(RecoveryBucket.LostWindowExpired));
        Assert.Equal((48, 8905.00m, 0m), Bucket(RecoveryBucket.LostPolicy));
    }

    [Fact]
    public void Recoverable_deadlines_and_top_priority()
    {
        var recoverable = Analyses.Where(a => a.Recoverability.Bucket == RecoveryBucket.Recoverable).ToList();
        Assert.Equal(5, recoverable.Count(a => a.Recoverability.DaysToDeadline <= 14));
        var dueIn30 = recoverable.Where(a => a.Recoverability.DaysToDeadline <= 30).ToList();
        Assert.Equal((15, 2890.00m), (dueIn30.Count, dueIn30.Sum(a => a.DeniedAmount)));
        Assert.Equal("GPP-2026-001828", Analyses.MaxBy(a => a.Recoverability.PriorityScore)!.ClaimId);
    }

    [Fact]
    public void Payer_errors_are_the_seven_recouped_sunshine_visits()
    {
        var payerErrors = Analyses.Where(a => a.Classification.RootCause == RootCauses.PayerError).ToList();
        Assert.Equal(7, payerErrors.Count);
        Assert.Contains(payerErrors, a => a.ClaimId == "GPP-2026-000230");
        Assert.All(payerErrors, a => Assert.Equal(RecoveryBucket.Recoverable, a.Recoverability.Bucket));
    }

    [Fact]
    public void All_classifications_are_confident_on_this_data() =>
        Assert.All(Analyses, a => Assert.Equal(Confidence.High, a.Classification.Confidence));

    [Fact]
    public void Rules_match_all_forty_expert_labels()
    {
        var predictions = Analyses.ToDictionary(a => a.ClaimId,
            a => new DenialPrediction(a.Classification.RootCause, a.Classification.OwningTeam, a.Classification.Preventable));

        var report = LabelEvaluator.Evaluate(Reference.Value.Labels, predictions);

        Assert.Equal((40, 40, 40, 40, 40, 40), (report.Labeled, report.Evaluated, report.RootCauseCorrect, report.OwningTeamCorrect, report.PreventableCorrect, report.FullyCorrect));
        Assert.Empty(report.Mismatches);
    }

    [Fact]
    public void Evaluator_reports_mismatches_and_missing_claims()
    {
        var labels = new[]
        {
            new LabeledDenial("A", RootCauses.Eligibility, OwningTeams.Eligibility, "Yes"),
            new LabeledDenial("B", RootCauses.Eligibility, OwningTeams.Eligibility, "Yes"),
        };
        var predictions = new Dictionary<string, DenialPrediction> { ["A"] = new(RootCauses.Authorization, OwningTeams.Eligibility, "yes") };

        var report = LabelEvaluator.Evaluate(labels, predictions);

        Assert.Equal((2, 1, 0, 1, 1, 0), (report.Labeled, report.Evaluated, report.RootCauseCorrect, report.OwningTeamCorrect, report.PreventableCorrect, report.FullyCorrect));
        Assert.Equal(new LabelMismatch("A", "root_cause_category", RootCauses.Eligibility, RootCauses.Authorization), Assert.Single(report.Mismatches));
        Assert.Equal(new[] { "B" }, report.Unevaluated);
    }

    [Fact]
    public void Unknown_payer_rule_is_routed_to_review()
    {
        var withoutNorthstar = Reference.Value.PayerRules.Where(r => r.Key != "NS401").ToDictionary(r => r.Key, r => r.Value);

        var analyses = DenialAnalyzer.Analyze(Pipeline.Value, withoutNorthstar, Today);

        var northstar = analyses.Where(a => a.PayerId == "NS401").ToList();
        Assert.NotEmpty(northstar);
        Assert.All(northstar, a => Assert.Null(a.Recoverability.Deadline));
        Assert.Contains(northstar, a => a.Classification.Confidence == Confidence.Low);
    }

    [Fact]
    public void Malformed_reference_csv_is_reported_without_row_content()
    {
        var dataDir = Directory.CreateTempSubdirectory().FullName;
        try
        {
            File.WriteAllText(Path.Combine(dataDir, "payer_rules.csv"),
                "payer,payer_id,timely_filing_days_from_dos,appeal_window_days_from_denial,corrected_claim_window_days_from_denial\n" +
                "Secret Health Plan,SECRET1,not-a-number,180,180\n");

            var error = Assert.Throws<InvalidDataException>(() => ReferenceDataLoader.Load(dataDir));

            Assert.Equal("payer_rules.csv could not be read.", error.Message);
            Assert.Null(error.InnerException);
        }
        finally
        {
            Directory.Delete(dataDir, recursive: true);
        }
    }
}
