using System.Text.RegularExpressions;
using DenialsCommandCenter.Domain.Analysis;
using DenialsCommandCenter.Domain.Claims;
using DenialsCommandCenter.Domain.Reference;

namespace DenialsCommandCenter.Domain.Appeals;

public sealed record ServiceLineFacts(string Cpt, string Modifier, decimal Charge, IReadOnlyList<string> DiagnosisCodes);

public sealed record DenialReasonFacts(string ProcedureCode, string Group, string Reason, string ReasonDescription, IReadOnlyList<string> Remarks, decimal Amount);

public sealed record RuleFindingFacts(string RootCause, string OwningTeam, string Preventable, string Action, string NextAction, IReadOnlyList<string> Citations);

public sealed record PolicySectionFacts(string Citation, string Text);

// Everything here is safe to send outside the system: no names, ids, NPIs or calendar dates.
public sealed record AppealFacts(
    string PayerId, string PayerName, string PlaceOfService, int DaysFromServiceToSubmission, bool ServiceDateBeforeSnfAuthorizationRequirement,
    IReadOnlyList<ServiceLineFacts> ServiceLines, IReadOnlyList<DenialReasonFacts> DenialReasons, decimal DeniedAmount,
    RuleFindingFacts? RuleFinding, IReadOnlyList<PolicySectionFacts> PolicySections);

public static class AppealFactsBuilder
{
    private const string Unknown = "UNKNOWN";
    private const string UnknownPayer = "Unknown payer";
    private const string ClaimLevel = "CLAIM";
    private static readonly Regex ReasonCodeShape = new(@"^[A-Z0-9]{1,4}\z", RegexOptions.CultureInvariant);
    private static readonly Regex RemarkCodeShape = new(@"^[A-Z]{1,2}[0-9]{1,4}\z", RegexOptions.CultureInvariant);
    private static readonly Regex ProcedureCodeShape = new(@"^[0-9A-Z]{5}\z", RegexOptions.CultureInvariant);
    private static readonly Regex ModifierShape = new(@"^[0-9A-Z]{2}\z", RegexOptions.CultureInvariant);
    private static readonly Regex DiagnosisCodeShape = new(@"^[A-TV-Z][0-9][0-9A-Z](\.?[0-9A-Z]{1,4})?\z", RegexOptions.CultureInvariant);
    private static readonly Regex PlaceOfServiceShape = new(@"^[0-9]{2}\z", RegexOptions.CultureInvariant);
    private static readonly HashSet<string> AdjustmentGroups = ["CO", "OA", "PI", "CR"];

    // Claim and remittance files are untrusted, so free fields are forwarded only when they have the expected shape,
    // and the payer is named from the payer rules rather than from the claim.
    public static AppealFacts Build(
        ClaimRecord claim, ClaimState state, DenialClassification? ruleFinding, PayerRule? payerRule, PolicyLibrary policies,
        ReasonCodeReference reasonCodes) => new(
        payerRule?.PayerId ?? Unknown,
        payerRule?.PayerName ?? UnknownPayer,
        Shaped(claim.PlaceOfService, PlaceOfServiceShape),
        claim.SubmittedDate.DayNumber - claim.DateOfService.DayNumber,
        claim.DateOfService < DenialClassifier.SnfAuthorizationEffective,
        claim.Lines.Select(ServiceLineOf).ToList(),
        state.DenialLines.Select(d => DenialReasonOf(d, reasonCodes)).ToList(),
        state.DeniedAmount,
        ruleFinding is null
            ? null
            : new RuleFindingFacts(ruleFinding.RootCause, ruleFinding.OwningTeam, ruleFinding.Preventable, ruleFinding.Action.ToString(),
                ruleFinding.NextAction, ruleFinding.Citations),
        policies.Documents.SelectMany(d => d.Sections).Select(s => new PolicySectionFacts(s.Citation, s.Text)).ToList());

    private static ServiceLineFacts ServiceLineOf(ClaimLine line) => new(
        Shaped(line.Cpt, ProcedureCodeShape),
        line.Modifier.Length == 0 ? "" : Shaped(line.Modifier, ModifierShape),
        line.Charge,
        line.DiagnosisCodes.Where(code => DiagnosisCodeShape.IsMatch(code)).ToList());

    private static DenialReasonFacts DenialReasonOf(DenialLine line, ReasonCodeReference reasonCodes)
    {
        var procedureCode = line.ProcedureCode == ClaimLevel ? ClaimLevel : Shaped(line.ProcedureCode, ProcedureCodeShape);
        var group = AdjustmentGroups.Contains(line.Group) ? line.Group : Unknown;
        var reason = Shaped(line.Reason, ReasonCodeShape);
        var remarks = line.Remarks.Where(remark => RemarkCodeShape.IsMatch(remark)).ToList();
        return new DenialReasonFacts(procedureCode, group, reason, reasonCodes.DescribeCarc(reason), remarks, line.Amount);
    }

    private static string Shaped(string value, Regex shape) => shape.IsMatch(value) ? value : Unknown;
}
