using System.Globalization;
using CsvHelper;

namespace DenialsCommandCenter.Domain.Reference;

public sealed record ReasonCodeReference(IReadOnlyDictionary<string, string> Carc, IReadOnlyDictionary<string, string> Rarc)
{
    public string DescribeCarc(string code) => Carc.TryGetValue(code, out var description) ? description : "Unknown reason code";
}

public static class ReasonCodeReader
{
    public static ReasonCodeReference Read(TextReader text)
    {
        using var csv = new CsvReader(text, CultureInfo.InvariantCulture);
        csv.Read();
        csv.ReadHeader();
        var carc = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var rarc = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        while (csv.Read())
        {
            var table = csv.GetField("type")!.Trim().Equals("RARC", StringComparison.OrdinalIgnoreCase) ? rarc : carc;
            table[csv.GetField("code")!.Trim()] = csv.GetField("description")!.Trim();
        }
        return new ReasonCodeReference(carc, rarc);
    }
}
