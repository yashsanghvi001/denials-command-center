using System.Globalization;
using DenialsCommandCenter.Domain;
using DenialsCommandCenter.Domain.Ingestion;
using DenialsCommandCenter.Domain.Worklog;

namespace DenialsCommandCenter.Api.Ingestion;

public static class DataPackLoader
{
    public static PipelineInput Load(string dataDir, DateOnly today)
    {
        var claimsCsv = File.ReadAllText(Path.Combine(dataDir, "claims_export.csv"));
        var remits = RemitFiles(dataDir).Select(p => new RemitSource(Path.GetFileName(p), File.ReadAllBytes(p))).ToList();
        using var xlsx = File.OpenRead(Path.Combine(dataDir, "denials_worklog.xlsx"));
        return new PipelineInput(claimsCsv, remits, WorklogReader.Read(xlsx), today);
    }

    // Today is part of the input: it decides which worklog date readings are possible.
    public static string InputHash(string dataDir, DateOnly today)
    {
        var fileParts = new[]
            {
                Path.Combine(dataDir, "claims_export.csv"), Path.Combine(dataDir, "denials_worklog.xlsx"),
                Path.Combine(dataDir, "payer_rules.csv"), Path.Combine(dataDir, "carc_rarc_reference.csv"),
            }
            .Concat(RemitFiles(dataDir))
            .Concat(Directory.GetFiles(Path.Combine(dataDir, "payer_policies"), "*.md"))
            .Select(p => $"{Path.GetRelativePath(dataDir, p).Replace('\\', '/')}:{Hashing.Sha256Hex(File.ReadAllBytes(p))}")
            .Order(StringComparer.Ordinal);
        var todayPart = $"today:{today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}";
        return Hashing.Sha256Hex(string.Join('\n', fileParts.Append(todayPart)));
    }

    private static IEnumerable<string> RemitFiles(string dataDir) =>
        Directory.GetFiles(Path.Combine(dataDir, "remits"))
            .Where(p => !Path.GetFileName(p).StartsWith('.'))
            .Order(StringComparer.Ordinal);
}
