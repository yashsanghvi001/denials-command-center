using DenialsCommandCenter.Domain.Analysis;
using DenialsCommandCenter.Domain.Claims;
using DenialsCommandCenter.Domain.X12;
using static DenialsCommandCenter.Tests.Analysis.AnalysisTestData;

namespace DenialsCommandCenter.Tests.Analysis;

public class RecoverabilityTests
{
    private static readonly DateOnly DateOfService = new(2026, 9, 20);

    private static DenialClassification Finding(NextActionType action) =>
        new("x", "y", "Yes", action, "do it", [], Confidence.High, null, ["11"]);

    private static Recoverability Assess(NextActionType action, string denialDate, string payerId = "NS401", decimal denied = 205m) =>
        RecoverabilityAssessor.Assess(DeniedState("GPP-2026-000001", denialDate, ("11", denied)), DateOfService, Finding(action),
            PayerRules.GetValueOrDefault(payerId), 0.6097m, Today);

    [Fact]
    public void Open_window_is_recoverable_with_deadline_expected_value_and_priority()
    {
        var result = Assess(NextActionType.Appeal, "2026-04-14", "NS401");

        Assert.Equal(RecoveryBucket.Recoverable, result.Bucket);
        Assert.Equal(new DateOnly(2026, 10, 11), result.Deadline);
        Assert.Equal(11, result.DaysToDeadline);
        Assert.Equal(124.99m, result.ExpectedValue);
        Assert.Equal(374.97m, result.PriorityScore);
    }

    [Fact]
    public void Corrected_claims_use_the_corrected_claim_window()
    {
        var rules = new Dictionary<string, Domain.Reference.PayerRule> { ["X1"] = new("X1", "Test", 90, 30, 120) };
        var state = DeniedState("GPP-2026-000001", "2026-08-01", ("11", 100m));
        Assert.Equal(new DateOnly(2026, 11, 29), RecoverabilityAssessor.Assess(state, DateOfService, Finding(NextActionType.CorrectedClaim), rules["X1"], 0.6m, Today).Deadline);
        Assert.Equal(new DateOnly(2026, 8, 31), RecoverabilityAssessor.Assess(state, DateOfService, Finding(NextActionType.Appeal), rules["X1"], 0.6m, Today).Deadline);
    }

    [Fact]
    public void Retro_authorization_deadline_is_the_earlier_of_the_retro_window_and_the_appeal_window()
    {
        var result = Assess(NextActionType.RequestRetroAuthorization, "2026-09-25", "SMP12");

        Assert.Equal(new DateOnly(2026, 10, 4), result.Deadline);
        Assert.Equal(4, result.DaysToDeadline);
        Assert.Equal(3, RecoverabilityAssessor.Urgency(result.DaysToDeadline));
    }

    [Fact]
    public void Deadline_today_is_still_recoverable()
    {
        var result = Assess(NextActionType.Appeal, "2026-04-03", "NS401");

        Assert.Equal((RecoveryBucket.Recoverable, Today, 0), (result.Bucket, result.Deadline!.Value, result.DaysToDeadline!.Value));
    }

    [Fact]
    public void Expected_value_rounds_half_to_even()
    {
        var state = DeniedState("GPP-2026-000001", "2026-09-01", ("11", 100.01m));

        var result = RecoverabilityAssessor.Assess(state, DateOfService, Finding(NextActionType.Appeal), PayerRules["NS401"], 0.5m, Today);

        Assert.Equal(50.00m, result.ExpectedValue);
    }

    [Fact]
    public void Passed_deadline_is_lost_whatever_the_action() =>
        Assert.Equal(RecoveryBucket.LostWindowExpired, Assess(NextActionType.Appeal, "2026-07-01", "SMP12").Bucket);

    [Theory]
    [InlineData(NextActionType.WriteOff, RecoveryBucket.LostPolicy)]
    [InlineData(NextActionType.CloseAsDuplicate, RecoveryBucket.LostPolicy)]
    [InlineData(NextActionType.BillOtherCoverage, RecoveryBucket.MaybeEligibility)]
    [InlineData(NextActionType.HumanReview, RecoveryBucket.Recoverable)]
    [InlineData(NextActionType.RequestRetroAuthorization, RecoveryBucket.Recoverable)]
    public void Open_window_bucket_follows_the_action(NextActionType action, RecoveryBucket expected)
    {
        var result = Assess(action, "2026-09-01");
        Assert.Equal(expected, result.Bucket);
        Assert.Equal(expected == RecoveryBucket.Recoverable, result.PriorityScore > 0);
        Assert.Equal(expected == RecoveryBucket.LostPolicy, result.ExpectedValue == 0);
    }

    [Fact]
    public void Lost_money_is_not_expected_back() =>
        Assert.Equal(0m, Assess(NextActionType.Appeal, "2026-07-01", "SMP12").ExpectedValue);

    [Fact]
    public void Unknown_payer_rule_has_no_deadline()
    {
        var result = Assess(NextActionType.HumanReview, "2026-09-01", "ZZ999");
        Assert.Null(result.Deadline);
        Assert.Equal(RecoveryBucket.Recoverable, result.Bucket);
        Assert.Equal(124.99m, result.ExpectedValue);
    }

    [Theory]
    [InlineData(0, 3)]
    [InlineData(14, 3)]
    [InlineData(15, 2)]
    [InlineData(30, 2)]
    [InlineData(31, 1)]
    [InlineData(null, 1)]
    public void Urgency_steps_at_14_and_30_days(int? days, int expected) => Assert.Equal(expected, RecoverabilityAssessor.Urgency(days));

    [Fact]
    public void Allowed_ratio_uses_paid_plus_patient_share_over_charge_of_paid_adjudications()
    {
        RemitEvent Event(string payerId, string status, decimal charge, decimal paid, decimal patient, string? claimId = "GPP-2026-000001") =>
            new("k" + Guid.NewGuid(), claimId, payerId, payerId, "T", Today, "f", 0,
                new ClaimPayment(1, "x", status, charge, paid, patient, "ICN", "1", "", "", "", "", null, [], [], []));

        var events = new[]
        {
            Event("NS401", "1", 100m, 50m, 10m),
            Event("NS401", "1", 100m, 60m, 0m),
            Event("NS401", "4", 100m, 0m, 0m),
            Event("NS401", "1", 100m, 100m, 0m, claimId: null),
            Event("SMP12", "1", 200m, 100m, 20m),
        };

        var ratios = AllowedRatios.ByPayer(events);
        Assert.Equal(0.6m, ratios["NS401"]);
        Assert.Equal(0.6m, ratios["SMP12"]);
        Assert.Equal(0.6m, AllowedRatios.Overall(events));
    }
}
