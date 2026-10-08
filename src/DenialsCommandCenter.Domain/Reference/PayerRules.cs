using System.Globalization;
using CsvHelper;

namespace DenialsCommandCenter.Domain.Reference;

public sealed record PayerRule(string PayerId, string PayerName, int TimelyFilingDays, int AppealWindowDays, int CorrectedClaimWindowDays);

public static class PayerRulesReader
{
    public static IReadOnlyDictionary<string, PayerRule> Read(TextReader text)
    {
        using var csv = new CsvReader(text, CultureInfo.InvariantCulture);
        csv.Read();
        csv.ReadHeader();
        var rules = new Dictionary<string, PayerRule>(StringComparer.OrdinalIgnoreCase);
        while (csv.Read())
        {
            var rule = new PayerRule(
                csv.GetField("payer_id")!.Trim(),
                csv.GetField("payer")!.Trim(),
                csv.GetField<int>("timely_filing_days_from_dos"),
                csv.GetField<int>("appeal_window_days_from_denial"),
                csv.GetField<int>("corrected_claim_window_days_from_denial"));
            rules[rule.PayerId] = rule;
        }
        return rules;
    }
}
