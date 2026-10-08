using DenialsCommandCenter.Domain.Analysis;
using DenialsCommandCenter.Domain.Claims;
using DenialsCommandCenter.Domain.Reference;

namespace DenialsCommandCenter.Tests.Analysis;

public static class AnalysisTestData
{
    public static readonly DateOnly Today = new(2026, 9, 30);

    public static readonly Dictionary<string, PayerRule> PayerRules = new(StringComparer.OrdinalIgnoreCase)
    {
        ["NS401"] = new("NS401", "Northstar Health Plan", 180, 180, 180),
        ["CSA77"] = new("CSA77", "Coastal Senior Advantage", 365, 120, 120),
        ["SMP12"] = new("SMP12", "Sunshine Medicaid Partners", 120, 60, 60),
        ["MRD55"] = new("MRD55", "Meridian PPO", 90, 90, 90),
    };

    public static ClaimRecord Claim(
        string claimId = "GPP-2026-000001", string payerId = "NS401", string dateOfService = "2026-05-10",
        string submitted = "2026-05-15", string npi = "1111111111", string memberId = "M1", string[]? cpts = null, string[]? diagnoses = null) =>
        new(claimId, "Jane", "Doe", new DateOnly(1940, 1, 2), memberId, PayerName(payerId), payerId,
            DateOnly.Parse(dateOfService, System.Globalization.CultureInfo.InvariantCulture),
            DateOnly.Parse(submitted, System.Globalization.CultureInfo.InvariantCulture),
            npi, "Dr Test", "St. Anselm Hospital", "21", "C01", false,
            (cpts is null || cpts.Length == 0 ? ["99232"] : cpts).Select((cpt, index) => new ClaimLine(index + 1, cpt, "", 1m, 140m, diagnoses ?? ["I10"], "")).ToList());

    public static ClaimState DeniedState(string claimId, string denialDate, params (string Reason, decimal Amount)[] reasons) =>
        new(claimId, ClaimStatus.Denied, 140m, 0m, reasons.Sum(r => r.Amount),
            DateOnly.Parse(denialDate, System.Globalization.CultureInfo.InvariantCulture), null,
            reasons.Select(r => new DenialLine("99232", "CO", r.Reason, r.Amount, [])).ToList(), false, 0m, 1, 0);

    public static DenialContext Context(ClaimRecord claim, ClaimState state, params ClaimRecord[] sameDay) =>
        new(claim, state, PayerRules.GetValueOrDefault(claim.PayerId), [claim, .. sameDay], Today);

    private static string PayerName(string payerId) => PayerRules.TryGetValue(payerId, out var rule) ? rule.PayerName : "Unknown Payer";
}
