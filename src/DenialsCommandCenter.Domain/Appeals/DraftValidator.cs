using System.Text.RegularExpressions;
using DenialsCommandCenter.Domain.Analysis;
using DenialsCommandCenter.Domain.Reference;

namespace DenialsCommandCenter.Domain.Appeals;

public sealed record AiDraft(string RootCause, string OwningTeam, string Preventable, string Confidence, string Letter, IReadOnlyList<string> CitedSections);

public sealed record DraftValidation(bool Accepted, string? RejectionReason);

public static class DraftValidator
{
    private const int MaxLetterLength = 6000;
    private const string AllPayersPolicyPrefix = "ALL_PAYERS_";
    private static readonly Regex Placeholder = new(@"\{\{[^{}]*\}\}", RegexOptions.CultureInvariant);
    private static readonly Regex CitationInText = new(@"(?<policy>[A-Z][A-Z0-9_-]+)\s*§\s*(?<section>[0-9]+)", RegexOptions.CultureInvariant);
    private static readonly Regex PossibleIdentifier = new(@"(?<![\$\d.,])\d(?:[\s\-./]?\d){5,}", RegexOptions.CultureInvariant);
    private static readonly string[] ConfidenceLevels = Enum.GetNames<Confidence>();

    public static DraftValidation Validate(AiDraft draft, PolicyLibrary policies)
    {
        if (HasMissingFields(draft))
            return Reject("The draft is missing required fields.");

        if (!RootCauses.All.Contains(draft.RootCause))
            return Reject($"Root cause '{draft.RootCause}' is not in the taxonomy.");
        if (!OwningTeams.All.Contains(draft.OwningTeam))
            return Reject($"Owning team '{draft.OwningTeam}' is not a known team.");
        if (!Preventability.All.Contains(draft.Preventable))
            return Reject($"Preventable value '{draft.Preventable}' is not Yes, No or Unknown.");
        if (!ConfidenceLevels.Contains(draft.Confidence))
            return Reject($"Confidence '{draft.Confidence}' is not High or Low.");

        if (string.IsNullOrWhiteSpace(draft.Letter))
            return Reject("The letter is empty.");
        if (draft.Letter.Length > MaxLetterLength)
            return Reject("The letter is too long.");

        var unknownCitation = CitationsIn(draft).FirstOrDefault(c => !policies.Citations.Contains(c));
        if (unknownCitation is not null)
            return Reject($"Cites '{unknownCitation}', which is not in the policy library.");

        var unknownPlaceholder = Placeholder.Matches(draft.Letter).Select(m => m.Value).FirstOrDefault(p => !LetterPlaceholders.All.Contains(p));
        if (unknownPlaceholder is not null)
            return Reject($"Uses unknown placeholder {unknownPlaceholder}.");

        var policyText = string.Join('\n', policies.Documents.SelectMany(d => d.Sections).Select(s => s.Text));
        var letterWithoutCitations = CitationInText.Replace(draft.Letter, " ");
        var possibleIdentifier = PossibleIdentifier.Matches(letterWithoutCitations).Select(m => m.Value).FirstOrDefault(v => !policyText.Contains(v, StringComparison.Ordinal));
        if (possibleIdentifier is not null)
            return Reject("The letter contains a long number that could be an identifier.");

        return new DraftValidation(true, null);
    }

    // A letter draft must agree with the rules engine: same root cause, and only the rule's own sections or all-payer policy.
    public static DraftValidation CheckAgainstRules(AiDraft draft, RuleFindingFacts ruleFinding)
    {
        if (draft.RootCause != ruleFinding.RootCause)
            return Reject($"AI root cause '{draft.RootCause}' disagrees with the rules ('{ruleFinding.RootCause}').");

        var citationOutsideRule = CitationsIn(draft)
            .FirstOrDefault(c => !ruleFinding.Citations.Contains(c) && !c.StartsWith(AllPayersPolicyPrefix, StringComparison.Ordinal));
        if (citationOutsideRule is not null)
            return Reject($"Cites '{citationOutsideRule}', which the rules do not cite for this denial.");

        return new DraftValidation(true, null);
    }

    private static IEnumerable<string> CitationsIn(AiDraft draft) =>
        draft.CitedSections.Concat(CitationInText.Matches(draft.Letter).Select(m => $"{m.Groups["policy"].Value} §{m.Groups["section"].Value}"));

    private static bool HasMissingFields(AiDraft draft) =>
        draft.RootCause is null || draft.OwningTeam is null || draft.Preventable is null || draft.Confidence is null
        || draft.Letter is null || draft.CitedSections is null || draft.CitedSections.Any(c => c is null);

    private static DraftValidation Reject(string reason) => new(false, reason);
}
