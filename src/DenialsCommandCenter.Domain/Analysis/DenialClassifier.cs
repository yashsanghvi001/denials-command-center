using System.Globalization;
using DenialsCommandCenter.Domain.Claims;

namespace DenialsCommandCenter.Domain.Analysis;

public static class DenialClassifier
{
    public const int RetroAuthorizationDays = 14;
    public static readonly DateOnly SnfAuthorizationEffective = new(2026, 4, 1);

    private const string Northstar = "NS401";
    private const string MeridianPpo = "MRD55";
    private const string SunshineMedicaid = "SMP12";
    private const string CoastalSeniorAdvantage = "CSA77";
    private const string EssentialHypertension = "I10";
    private const string HypertensiveHeartDiseasePrefix = "I11";
    private const string SeparateEvaluationModifier = "25";
    private const string MedicalRecordMissingRemark = "M127";
    private static readonly HashSet<string> InitialNursingFacilityCodes = ["99304", "99305", "99306"];

    public static DenialClassification Classify(DenialContext context)
    {
        var denialLines = context.State.DenialLines;
        var reasonCodes = denialLines.Select(l => l.Reason).Distinct().Order(StringComparer.Ordinal).ToList();
        if (reasonCodes.Count == 0)
            return Review(reasonCodes, "The remittance denies the claim without a denial reason code.");

        var primaryReason = denialLines
            .GroupBy(l => l.Reason)
            .OrderByDescending(g => g.Sum(l => l.Amount))
            .ThenBy(g => g.Key, StringComparer.Ordinal)
            .First().Key;
        var classification = ClassifyReason(primaryReason, context) with { ReasonCodes = reasonCodes };

        if (reasonCodes.Count > 1 && classification.Confidence == Confidence.High)
            classification = classification with
            {
                Confidence = Confidence.Low,
                ConfidenceReason = $"Several denial reasons ({string.Join(", ", reasonCodes)}); classified by the largest.",
            };
        return classification;
    }

    private static DenialClassification ClassifyReason(string reason, DenialContext context) => reason switch
    {
        "11" => DiagnosisExclusion(context),
        "97" => BundledEvaluation(context),
        "151" => Frequency(context),
        "197" => Authorization(context),
        "B7" => Rule(RootCauses.Credentialing, OwningTeams.Credentialing, Preventability.Yes, NextActionType.WriteOff,
            "Write off: enrollment is not retroactive. Finish the provider's enrollment and hold their claims for this payer until it is effective.",
            context.Claim.PayerId == CoastalSeniorAdvantage
                ? ["CSA_PROVIDER-ENROLLMENT §1", "CSA_PROVIDER-ENROLLMENT §3"]
                : []),
        "27" => Rule(RootCauses.Eligibility, OwningTeams.Eligibility, Preventability.Yes, NextActionType.BillOtherCoverage,
            "Verify the patient's coverage on the date of service, then bill the active payer or the patient."),
        "29" => TimelyFiling(context),
        "50" => MedicalNecessity(context),
        "18" => Rule(RootCauses.Duplicate, OwningTeams.Billing, Preventability.Yes, NextActionType.CloseAsDuplicate,
            "Confirm the original claim was paid, then close this duplicate; nothing further to recover."),
        _ => Review([], $"Denial reason {reason} has no rule."),
    };

    private static DenialClassification DiagnosisExclusion(DenialContext context)
    {
        var classification = Rule(RootCauses.CodingDiagnosis, OwningTeams.Coding, Preventability.Yes, NextActionType.CorrectedClaim,
            "Send a corrected (replacement) claim that removes I10 and keeps the more specific I11.- code.",
            context.Claim.PayerId == MeridianPpo
                ? ["MPPO_DX-EXCL-03 §2", "MPPO_DX-EXCL-03 §3", "MPPO_DX-EXCL-03 §4"]
                : ["MPPO_DX-EXCL-03 §2", "MPPO_DX-EXCL-03 §3"]);
        var diagnoses = context.Claim.Lines.SelectMany(l => l.DiagnosisCodes).ToList();
        return diagnoses.Contains(EssentialHypertension)
               && diagnoses.Any(code => code.StartsWith(HypertensiveHeartDiseasePrefix, StringComparison.Ordinal))
            ? classification
            : WithLowConfidence(classification, "The claim does not report I10 together with an I11.- code.");
    }

    private static DenialClassification BundledEvaluation(DenialContext context)
    {
        var classification = Rule(RootCauses.CodingModifier, OwningTeams.Coding, Preventability.Yes, NextActionType.CorrectedClaim,
            "Send a corrected (replacement) claim with modifier 25 on the E/M line when the visit note supports a separate E/M service.",
            "ALL_PAYERS_MOD25-2026 §1", "ALL_PAYERS_MOD25-2026 §2", "ALL_PAYERS_MOD25-2026 §3");
        var lines = context.Claim.Lines;
        var evaluationLines = lines.Where(IsEvaluationAndManagement).ToList();
        if (evaluationLines.Count == 0 || evaluationLines.Count == lines.Count)
            return WithLowConfidence(classification, "The claim does not have both an E/M line and a procedure line.");
        if (evaluationLines.Any(l => l.Modifier == SeparateEvaluationModifier))
            return WithLowConfidence(classification, "The E/M line already carries modifier 25.");
        return classification;
    }

    private static DenialClassification MedicalNecessity(DenialContext context)
    {
        var classification = Rule(RootCauses.MedicalNecessity, OwningTeams.Clinical, Preventability.No, NextActionType.Appeal,
            "Appeal with the medical record for the date of service; the payer reported the record missing.");
        return context.State.DenialLines.Any(l => l.Remarks.Contains(MedicalRecordMissingRemark))
            ? classification
            : WithLowConfidence(classification, "The remittance does not report the medical record missing (RARC M127).");
    }

    private static DenialClassification Frequency(DenialContext context)
    {
        var claim = context.Claim;
        var sameProviderSameDay = context.SameDayClaimsForPatient.Any(c => c.ClaimId != claim.ClaimId && c.RenderingNpi == claim.RenderingNpi);
        var classification = sameProviderSameDay
            ? Rule(RootCauses.CodingFrequency, OwningTeams.Coding, Preventability.Yes, NextActionType.WriteOff,
                "Write off: the same provider already billed this patient for this date, so this visit claim is a duplicate.",
                "NSHP_HOSP-FREQ-07 §1", "NSHP_HOSP-FREQ-07 §2")
            : Rule(RootCauses.CodingFrequency, OwningTeams.Coding, Preventability.Yes, NextActionType.Appeal,
                "Appeal only if the record shows a significant change in condition: rebill with modifier 25 and a distinct diagnosis, and attach both progress notes.",
                "NSHP_HOSP-FREQ-07 §2", "NSHP_HOSP-FREQ-07 §3", "NSHP_HOSP-FREQ-07 §4");
        return claim.PayerId == Northstar
            ? classification
            : WithLowConfidence(classification with { Citations = [] }, "The frequency policy on file is Northstar's; this payer's rule is not on file.");
    }

    private static DenialClassification Authorization(DenialContext context)
    {
        var claim = context.Claim;
        var isSunshine = claim.PayerId == SunshineMedicaid;
        var isInitialVisit = claim.Lines.Any(l => InitialNursingFacilityCodes.Contains(l.Cpt));
        if (isSunshine && isInitialVisit && claim.DateOfService < SnfAuthorizationEffective)
            return Rule(RootCauses.PayerError, OwningTeams.DenialsAppeal, Preventability.No, NextActionType.Appeal,
                $"Appeal: the authorization requirement starts with dates of service on or after {SnfAuthorizationEffective.ToString("MMM d, yyyy", CultureInfo.InvariantCulture)}, and this visit is earlier.",
                "SMP_SNF-AUTH-2026 §2", "SMP_SNF-AUTH-2026 §3", "SMP_SNF-AUTH-2026 §5");

        string[] citations = isSunshine ? ["SMP_SNF-AUTH-2026 §2", "SMP_SNF-AUTH-2026 §4"] : [];
        return context.Today <= claim.DateOfService.AddDays(RetroAuthorizationDays)
            ? Rule(RootCauses.Authorization, OwningTeams.Authorization, Preventability.Yes, NextActionType.RequestRetroAuthorization,
                "Request retro-authorization for the facility admission, then resubmit with the authorization number.", citations)
            : Rule(RootCauses.Authorization, OwningTeams.Authorization, Preventability.Yes, NextActionType.WriteOff,
                $"Write off: the {RetroAuthorizationDays}-day retro-authorization window has passed. Report the admission authorization number on future initial visits.", citations);
    }

    private static DenialClassification TimelyFiling(DenialContext context)
    {
        if (context.PayerRule is null)
            return Review([], "No timely-filing limit is configured for this payer.");
        var claim = context.Claim;
        var daysToSubmit = claim.SubmittedDate.DayNumber - claim.DateOfService.DayNumber;
        var limit = context.PayerRule.TimelyFilingDays;
        return daysToSubmit > limit
            ? Rule(RootCauses.TimelyFiling, OwningTeams.Billing, Preventability.Yes, NextActionType.WriteOff,
                $"Write off: submitted {daysToSubmit} days after the date of service; the payer allows {limit}.")
            : Rule(RootCauses.TimelyFiling, OwningTeams.Billing, Preventability.Yes, NextActionType.Appeal,
                $"Appeal with proof of timely submission: it went out {daysToSubmit} days after the date of service, inside the payer's {limit}-day limit.");
    }

    private static bool IsEvaluationAndManagement(ClaimLine line) =>
        int.TryParse(line.Cpt, NumberStyles.None, CultureInfo.InvariantCulture, out var code) && code is >= 99202 and <= 99499;

    private static DenialClassification WithLowConfidence(DenialClassification classification, string reason) =>
        classification with { Confidence = Confidence.Low, ConfidenceReason = reason };

    private static DenialClassification Rule(
        string rootCause, string owningTeam, string preventable, NextActionType action, string nextAction, params string[] citations) =>
        new(rootCause, owningTeam, preventable, action, nextAction, citations, Confidence.High, null, []);

    private static DenialClassification Review(IReadOnlyList<string> reasonCodes, string reason) =>
        new(RootCauses.Unclassified, OwningTeams.DenialsAppeal, Preventability.Unknown, NextActionType.HumanReview,
            "Route to a denials specialist for manual review.", [], Confidence.Low, reason, reasonCodes);
}
