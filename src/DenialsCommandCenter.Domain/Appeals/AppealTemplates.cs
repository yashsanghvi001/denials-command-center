using System.Globalization;
using System.Text;
using DenialsCommandCenter.Domain.Analysis;
using DenialsCommandCenter.Domain.Reference;

namespace DenialsCommandCenter.Domain.Appeals;

public static class AppealTemplates
{
    public static string? Letter(DenialClassification finding, string payerName, decimal deniedAmount, PolicyLibrary policies)
    {
        (string Opening, string Basis)? wording = finding.Action switch
        {
            NextActionType.Appeal => ("We request reconsideration of the denial below.",
                "Please reprocess this claim under the policy below."),
            NextActionType.CorrectedClaim => ("We are submitting a corrected claim (frequency code 7) to replace the denied claim below.",
                "The corrected claim addresses the reason given in your denial."),
            NextActionType.RequestRetroAuthorization => ("We request retro-authorization for the service below so the claim can be reprocessed.",
                "Authorization documentation is enclosed."),
            _ => null,
        };
        if (wording is not { } payerWording) return null;

        var letter = new StringBuilder();
        letter.AppendLine($"To: {payerName}, Claims Reconsideration");
        letter.AppendLine($"Re: Claim {LetterPlaceholders.ClaimId} for {LetterPlaceholders.PatientName} (member {LetterPlaceholders.MemberId}), " +
                          $"date of service {LetterPlaceholders.DateOfService}, denied {LetterPlaceholders.DenialDate}");
        letter.AppendLine();
        letter.AppendLine(payerWording.Opening);
        letter.AppendLine($"Amount denied: ${deniedAmount.ToString("0.00", CultureInfo.InvariantCulture)}.");
        letter.AppendLine(payerWording.Basis);
        var citedSections = finding.Citations.Select(citation => policies.Find(citation)).OfType<PolicySection>().ToList();
        foreach (var section in citedSections)
            letter.AppendLine($"{section.Citation}: \"{section.Text}\"");
        if (citedSections.Count == 0)
            letter.AppendLine("Supporting documentation is enclosed.");
        letter.AppendLine();
        letter.AppendLine($"{LetterPlaceholders.ProviderName}, NPI {LetterPlaceholders.ProviderNpi}");
        return letter.ToString().TrimEnd();
    }
}
