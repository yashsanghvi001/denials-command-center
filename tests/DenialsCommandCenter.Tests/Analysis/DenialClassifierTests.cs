using DenialsCommandCenter.Domain.Analysis;
using static DenialsCommandCenter.Tests.Analysis.AnalysisTestData;

namespace DenialsCommandCenter.Tests.Analysis;

public class DenialClassifierTests
{
    private static DenialClassification Classify(
        string reason, string payerId = "NS401", string dateOfService = "2026-05-10", string submitted = "2026-05-15", string[]? cpts = null,
        string[]? diagnoses = null)
    {
        var claim = Claim(payerId: payerId, dateOfService: dateOfService, submitted: submitted, cpts: cpts, diagnoses: diagnoses);
        return DenialClassifier.Classify(Context(claim, DeniedState(claim.ClaimId, "2026-08-01", (reason, 140m))));
    }

    private static DenialClassification ClassifyWithRemarks(string reason, params string[] remarks)
    {
        var claim = Claim();
        var state = DeniedState(claim.ClaimId, "2026-08-01", (reason, 140m));
        return DenialClassifier.Classify(Context(claim, state with { DenialLines = [state.DenialLines[0] with { Remarks = remarks }] }));
    }

    private static void AssertLowConfidence(DenialClassification result)
    {
        Assert.Equal(Confidence.Low, result.Confidence);
        Assert.False(string.IsNullOrWhiteSpace(result.ConfidenceReason));
    }

    [Fact]
    public void Excludes1_diagnosis_is_a_coding_correction()
    {
        var result = Classify("11", diagnoses: ["I10", "I11.9"]);
        Assert.Equal((RootCauses.CodingDiagnosis, OwningTeams.Coding, Preventability.Yes, NextActionType.CorrectedClaim),
            (result.RootCause, result.OwningTeam, result.Preventable, result.Action));
        Assert.Equal(new[] { "MPPO_DX-EXCL-03 §2", "MPPO_DX-EXCL-03 §3" }, result.Citations);
        Assert.Equal(Confidence.High, result.Confidence);
        Assert.Equal(new[] { "11" }, result.ReasonCodes);
    }

    [Fact]
    public void Excludes1_at_meridian_also_cites_meridians_corrected_claim_deadline() =>
        Assert.Equal(new[] { "MPPO_DX-EXCL-03 §2", "MPPO_DX-EXCL-03 §3", "MPPO_DX-EXCL-03 §4" },
            Classify("11", payerId: "MRD55", diagnoses: ["I10", "I11.9"]).Citations);

    [Fact]
    public void Excludes1_denial_without_both_hypertension_codes_has_low_confidence()
    {
        var result = Classify("11", diagnoses: ["I10", "E11.65"]);

        Assert.Equal(NextActionType.CorrectedClaim, result.Action);
        AssertLowConfidence(result);
    }

    [Fact]
    public void Bundled_em_is_a_modifier_correction()
    {
        var result = Classify("97", cpts: ["99232", "93010"]);

        Assert.Equal((RootCauses.CodingModifier, NextActionType.CorrectedClaim, Confidence.High), (result.RootCause, result.Action, result.Confidence));
    }

    [Theory]
    [InlineData("99232")]
    [InlineData("93010")]
    public void Bundled_em_without_both_an_em_and_a_procedure_line_has_low_confidence(string cpt)
    {
        var result = Classify("97", cpts: [cpt]);

        Assert.Equal(NextActionType.CorrectedClaim, result.Action);
        AssertLowConfidence(result);
    }

    [Fact]
    public void Bundled_em_that_already_has_modifier_25_has_low_confidence()
    {
        var claim = Claim(cpts: ["99232", "93010"]);
        claim = claim with { Lines = [claim.Lines[0] with { Modifier = "25" }, claim.Lines[1]] };

        var result = DenialClassifier.Classify(Context(claim, DeniedState(claim.ClaimId, "2026-08-01", ("97", 140m))));

        Assert.Equal(NextActionType.CorrectedClaim, result.Action);
        AssertLowConfidence(result);
    }

    [Fact]
    public void Sunshine_initial_snf_visit_before_april_is_payer_error()
    {
        var result = Classify("197", payerId: "SMP12", dateOfService: "2026-02-23", cpts: ["99305"]);
        Assert.Equal((RootCauses.PayerError, OwningTeams.DenialsAppeal, Preventability.No, NextActionType.Appeal),
            (result.RootCause, result.OwningTeam, result.Preventable, result.Action));
        Assert.Contains("SMP_SNF-AUTH-2026 §3", result.Citations);
    }

    [Fact]
    public void Missing_authorization_after_april_past_retro_window_is_written_off()
    {
        var result = Classify("197", payerId: "SMP12", dateOfService: "2026-06-01", cpts: ["99305"]);
        Assert.Equal((RootCauses.Authorization, OwningTeams.Authorization, Preventability.Yes, NextActionType.WriteOff),
            (result.RootCause, result.OwningTeam, result.Preventable, result.Action));
    }

    [Fact]
    public void Missing_authorization_inside_retro_window_requests_retro_authorization() =>
        Assert.Equal(NextActionType.RequestRetroAuthorization,
            Classify("197", payerId: "SMP12", dateOfService: "2026-09-20", cpts: ["99305"]).Action);

    [Fact]
    public void Not_enrolled_provider_is_written_off_and_cites_enrollment_policy_only_for_coastal()
    {
        var coastal = Classify("B7", payerId: "CSA77");
        Assert.Equal((RootCauses.Credentialing, OwningTeams.Credentialing, NextActionType.WriteOff), (coastal.RootCause, coastal.OwningTeam, coastal.Action));
        Assert.Contains("CSA_PROVIDER-ENROLLMENT §3", coastal.Citations);
        Assert.Empty(Classify("B7", payerId: "MRD55").Citations);
    }

    [Fact]
    public void Coverage_terminated_bills_other_coverage() =>
        Assert.Equal((RootCauses.Eligibility, OwningTeams.Eligibility, NextActionType.BillOtherCoverage),
            (Classify("27").RootCause, Classify("27").OwningTeam, Classify("27").Action));

    [Fact]
    public void Late_submission_is_written_off_and_on_time_submission_is_appealed()
    {
        var late = Classify("29", payerId: "MRD55", dateOfService: "2026-03-03", submitted: "2026-07-01");
        Assert.Equal((RootCauses.TimelyFiling, OwningTeams.Billing, NextActionType.WriteOff), (late.RootCause, late.OwningTeam, late.Action));
        Assert.Contains("120 days", late.NextAction);
        Assert.Equal(NextActionType.Appeal, Classify("29", payerId: "MRD55", dateOfService: "2026-03-03", submitted: "2026-03-10").Action);
    }

    [Fact]
    public void Medical_necessity_is_clinical_and_not_preventable()
    {
        var result = ClassifyWithRemarks("50", "M127");
        Assert.Equal((RootCauses.MedicalNecessity, OwningTeams.Clinical, Preventability.No, NextActionType.Appeal),
            (result.RootCause, result.OwningTeam, result.Preventable, result.Action));
        Assert.Equal(Confidence.High, result.Confidence);
    }

    [Fact]
    public void Medical_necessity_without_the_missing_record_remark_has_low_confidence()
    {
        var result = ClassifyWithRemarks("50", "N54");

        Assert.Equal(NextActionType.Appeal, result.Action);
        AssertLowConfidence(result);
    }

    [Fact]
    public void Exact_duplicate_is_closed() => Assert.Equal(NextActionType.CloseAsDuplicate, Classify("18").Action);

    [Fact]
    public void Frequency_denial_by_another_provider_is_appealed_but_same_provider_is_written_off()
    {
        var claim = Claim(claimId: "GPP-2026-000002", npi: "222");
        var state = DeniedState(claim.ClaimId, "2026-08-01", ("151", 205m));
        var otherProvider = Claim(claimId: "GPP-2026-000003", npi: "333");
        var sameProvider = Claim(claimId: "GPP-2026-000004", npi: "222");

        var appealed = DenialClassifier.Classify(Context(claim, state, otherProvider));
        var writtenOff = DenialClassifier.Classify(Context(claim, state, sameProvider));

        Assert.Equal((RootCauses.CodingFrequency, NextActionType.Appeal), (appealed.RootCause, appealed.Action));
        Assert.Contains("NSHP_HOSP-FREQ-07 §3", appealed.Citations);
        Assert.Equal((RootCauses.CodingFrequency, NextActionType.WriteOff), (writtenOff.RootCause, writtenOff.Action));
    }

    [Fact]
    public void Frequency_denial_outside_northstar_has_no_citations_and_low_confidence()
    {
        var result = Classify("151", payerId: "CSA77");

        Assert.Equal((RootCauses.CodingFrequency, NextActionType.Appeal), (result.RootCause, result.Action));
        Assert.Empty(result.Citations);
        AssertLowConfidence(result);
        Assert.Contains("Northstar", result.ConfidenceReason);
    }

    [Fact]
    public void Unknown_reason_goes_to_human_review()
    {
        var result = Classify("45X");
        Assert.Equal((RootCauses.Unclassified, NextActionType.HumanReview, Confidence.Low), (result.RootCause, result.Action, result.Confidence));
        Assert.Equal(new[] { "45X" }, result.ReasonCodes);
    }

    [Fact]
    public void Several_reasons_are_classified_by_the_largest_with_low_confidence()
    {
        var claim = Claim();
        var result = DenialClassifier.Classify(Context(claim, DeniedState(claim.ClaimId, "2026-08-01", ("97", 140m), ("11", 260m))));

        Assert.Equal(RootCauses.CodingDiagnosis, result.RootCause);
        Assert.Equal(Confidence.Low, result.Confidence);
        Assert.Equal(new[] { "11", "97" }, result.ReasonCodes);
    }

    [Fact]
    public void Denial_without_reason_codes_goes_to_human_review() =>
        Assert.Equal(NextActionType.HumanReview, DenialClassifier.Classify(Context(Claim(), DeniedState("GPP-2026-000001", "2026-08-01"))).Action);

    [Theory]
    [InlineData("2026-09-16", NextActionType.RequestRetroAuthorization)]
    [InlineData("2026-09-15", NextActionType.WriteOff)]
    public void Retro_authorization_window_boundary_for_snf(string dateOfService, NextActionType expectedAction)
    {
        var result = Classify("197", payerId: "SMP12", dateOfService: dateOfService, cpts: ["99305"]);
        Assert.Equal(expectedAction, result.Action);
    }

    [Theory]
    [InlineData("2026-03-02", 90, NextActionType.Appeal)]
    [InlineData("2026-03-01", 91, NextActionType.WriteOff)]
    public void Timely_filing_boundary_at_limit(string dateOfService, int daysSubmitted, NextActionType expectedAction)
    {
        var submitted = DateOnly.Parse(dateOfService, System.Globalization.CultureInfo.InvariantCulture).AddDays(daysSubmitted).ToString("O");
        var result = Classify("29", payerId: "MRD55", dateOfService: dateOfService, submitted: submitted);
        Assert.Equal(expectedAction, result.Action);
        if (expectedAction == NextActionType.WriteOff)
        {
            Assert.Contains("91 days", result.NextAction);
            Assert.Contains("90", result.NextAction);
        }
    }

    [Theory]
    [InlineData("2026-03-31", RootCauses.PayerError, NextActionType.Appeal)]
    [InlineData("2026-04-01", RootCauses.Authorization, NextActionType.WriteOff)]
    public void SNF_authorization_effective_date_boundary(string dateOfService, string expectedRootCause, NextActionType expectedAction)
    {
        var result = Classify("197", payerId: "SMP12", dateOfService: dateOfService, cpts: ["99305"]);
        Assert.Equal(expectedRootCause, result.RootCause);
        Assert.Equal(expectedAction, result.Action);
    }

    [Fact]
    public void SMP12_pre_april_with_non_initial_cpt_is_authorization()
    {
        var result = Classify("197", payerId: "SMP12", dateOfService: "2026-03-15", cpts: ["99232"]);
        Assert.Equal(RootCauses.Authorization, result.RootCause);
        Assert.Equal(NextActionType.WriteOff, result.Action);
    }

    [Fact]
    public void Initial_snf_cpt_at_ns401_before_april_has_no_citations()
    {
        var result = Classify("197", payerId: "NS401", dateOfService: "2026-03-15", cpts: ["99305"]);
        Assert.Equal(RootCauses.Authorization, result.RootCause);
        Assert.Empty(result.Citations);
    }

    [Fact]
    public void Timely_filing_with_null_payer_rule_goes_to_human_review()
    {
        var claim = Claim();
        var state = DeniedState(claim.ClaimId, "2026-08-01", ("29", 140m));
        var context = new DenialContext(claim, state, null, [claim], Today);
        var result = DenialClassifier.Classify(context);
        Assert.Equal(NextActionType.HumanReview, result.Action);
        Assert.Equal(Confidence.Low, result.Confidence);
    }
}
