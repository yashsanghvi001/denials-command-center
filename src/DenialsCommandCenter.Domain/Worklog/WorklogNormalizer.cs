using System.Globalization;
using System.Text.RegularExpressions;
using DenialsCommandCenter.Domain.Claims;
using DenialsCommandCenter.Domain.Ingestion;

namespace DenialsCommandCenter.Domain.Worklog;

public static class WorklogNormalizer
{
    private static readonly Dictionary<string, string> PayerAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["NSHP"] = "Northstar Health Plan", ["Northstar Health Plan"] = "Northstar Health Plan",
        ["CSA"] = "Coastal Senior Advantage", ["Coastal Senior Advantage"] = "Coastal Senior Advantage",
        ["SMP"] = "Sunshine Medicaid Partners", ["Sunshine Medicaid Partners"] = "Sunshine Medicaid Partners",
        ["MPPO"] = "Meridian PPO", ["Meridian PPO"] = "Meridian PPO",
    };

    private static readonly Regex Suspicious = new(
        @"ignore\s+(all\s+)?(previous|prior|above)\s+instructions|disregard\s+.*instructions|system\s+(note|prompt|message)|\b(ai|llm)\s+assistant\b|you\s+are\s+now",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex SlashDate = new(@"^(\d{1,2})/(\d{1,2})/(\d{4})$", RegexOptions.CultureInvariant);
    private static readonly string[] ExactDateFormats = ["yyyy-MM-dd", "dd-MMM-yy", "d-MMM-yy"];

    public static (IReadOnlyList<WorklogEntry> Entries, IReadOnlyList<IngestionIssue> Issues) Normalize(
        IReadOnlyList<WorklogRawRow> rows, ClaimMatcher matcher, IReadOnlyDictionary<string, DateOnly> firstRemitDateByClaim,
        DateOnly today, string source = "denials_worklog.xlsx")
    {
        var issues = new List<IngestionIssue>();
        var entries = new List<WorklogEntry>();
        var seen = new HashSet<string>();

        foreach (var row in rows)
        {
            var reference = $"row {row.RowNumber}";
            var duplicateKey = string.Join('|',
                row.Logged.Trim().ToUpperInvariant(),
                row.Claim.Trim(),
                row.Patient.Trim().ToUpperInvariant(),
                row.Payer.Trim().ToUpperInvariant(),
                row.Amount.Trim().ToUpperInvariant(),
                row.Notes.Trim().ToUpperInvariant(),
                row.Owner.Trim().ToUpperInvariant(),
                row.Status.Trim().ToUpperInvariant());
            if (!seen.Add(duplicateKey))
            {
                issues.Add(new(IssueKinds.WorklogDuplicateRow, Severity.Warning, source, reference, "This row is an exact copy of an earlier worklog row, so it was ignored."));
                continue;
            }

            var match = matcher.Match(row.Claim);
            if (match.ClaimId is null)
                issues.Add(new(IssueKinds.WorklogUnmatchedClaim, Severity.Error, source, reference, match.FailureReason!));

            var candidates = DateCandidates(row.Logged, today);
            DateOnly? logged = null;
            if (candidates.Count == 1)
            {
                logged = candidates[0];
            }
            else if (candidates.Count > 1)
            {
                var fitting = match.ClaimId is not null && firstRemitDateByClaim.TryGetValue(match.ClaimId, out var firstRemit)
                    ? candidates.Where(c => c >= firstRemit).ToList()
                    : new List<DateOnly>();
                if (fitting.Count == 1) logged = fitting[0];
                else issues.Add(new(IssueKinds.WorklogAmbiguousDate, Severity.Warning, source, reference,
                    $"The date '{row.Logged}' could mean {string.Join(" or ", candidates.Select(c => c.ToString("MMM d, yyyy", CultureInfo.InvariantCulture)))}; confirm which one.", match.ClaimId));
            }
            else
            {
                issues.Add(new(IssueKinds.WorklogUnparseableDate, Severity.Warning, source, reference, $"'{row.Logged}' is not a date we can read.", match.ClaimId));
            }

            var suspicious = IsSuspicious(row.Notes);
            if (suspicious)
                issues.Add(new(IssueKinds.WorklogSuspiciousText, Severity.Warning, source, reference,
                    "The note contains text that tries to give instructions to an AI system. It is kept as plain text and never followed.", match.ClaimId));

            entries.Add(new WorklogEntry(
                row.RowNumber, match.ClaimId, logged, candidates, row.Patient.Trim(),
                PayerAliases.TryGetValue(row.Payer.Trim(), out var payer) ? payer : null,
                ParseAmount(row.Amount), row.Notes.Trim(), NormalizeOwner(row.Owner), MapStatus(row.Status), row.Status.Trim(), suspicious));
        }
        return (entries, issues);
    }

    public static IReadOnlyList<DateOnly> DateCandidates(string raw, DateOnly today)
    {
        var trimmed = raw.Trim();
        if (DateOnly.TryParseExact(trimmed, ExactDateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var exact))
            return [exact];

        var match = SlashDate.Match(trimmed);
        if (!match.Success) return [];
        int first = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        int second = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
        int year = int.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture);
        if (year is < 1 or > 9999) return [];

        var result = new List<DateOnly>();
        foreach (var (month, day) in new[] { (first, second), (second, first) })
        {
            if (month is < 1 or > 12 || day < 1 || day > DateTime.DaysInMonth(year, month)) continue;
            var candidate = new DateOnly(year, month, day);
            if (candidate <= today && !result.Contains(candidate)) result.Add(candidate);
        }
        return result;
    }

    public static WorklogStatus MapStatus(string raw) => raw.Trim().ToLowerInvariant() switch
    {
        "open" => WorklogStatus.Open,
        "in progress" or "wip" => WorklogStatus.InProgress,
        "pending w/ payer" or "pending with payer" => WorklogStatus.PendingPayer,
        "resolved" or "closed" or "done" => WorklogStatus.Closed,
        _ => WorklogStatus.Unknown,
    };

    public static bool IsSuspicious(string text) => Suspicious.IsMatch(text);

    private static decimal? ParseAmount(string raw) =>
        decimal.TryParse(raw.Replace("$", "").Replace(",", "").Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var amount) ? amount : null;

    private static string? NormalizeOwner(string raw)
    {
        var owner = raw.Trim();
        return owner.Length == 0 ? null : CultureInfo.InvariantCulture.TextInfo.ToTitleCase(owner.ToLowerInvariant());
    }
}
