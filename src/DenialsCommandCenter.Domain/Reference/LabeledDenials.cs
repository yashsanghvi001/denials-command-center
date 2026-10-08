using System.Globalization;
using CsvHelper;

namespace DenialsCommandCenter.Domain.Reference;

public sealed record LabeledDenial(string ClaimId, string RootCause, string OwningTeam, string Preventable);

public static class LabeledDenialsReader
{
    public static IReadOnlyList<LabeledDenial> Read(TextReader text)
    {
        using var csv = new CsvReader(text, CultureInfo.InvariantCulture);
        csv.Read();
        csv.ReadHeader();
        var labels = new List<LabeledDenial>();
        while (csv.Read())
            labels.Add(new LabeledDenial(
                csv.GetField("claim_id")!.Trim(),
                csv.GetField("root_cause_category")!.Trim(),
                csv.GetField("owning_team")!.Trim(),
                csv.GetField("preventable_at_prebill")!.Trim()));
        return labels;
    }
}
