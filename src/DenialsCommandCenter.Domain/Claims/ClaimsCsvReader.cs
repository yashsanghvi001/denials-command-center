using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using DenialsCommandCenter.Domain.Ingestion;

namespace DenialsCommandCenter.Domain.Claims;

public static class ClaimsCsvReader
{
    private static readonly string[] ClaimLevelFields =
    [
        "patient_first", "patient_last", "patient_dob", "member_id", "payer", "payer_id", "dos", "submitted_date",
        "rendering_npi", "rendering_provider", "facility", "pos", "coder_id", "prebill_reviewed",
    ];

    private static readonly string[] RequiredColumns =
        ["claim_id", .. ClaimLevelFields, "line_no", "cpt", "modifier", "units", "charge", "dx1", "dx2", "dx3", "dx4", "auth_number"];

    public static (IReadOnlyList<ClaimRecord> Claims, IReadOnlyList<IngestionIssue> Issues) Read(TextReader text, string source = "claims_export.csv")
    {
        var rows = new List<(int RowNumber, Dictionary<string, string> Fields)>();
        var configuration = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            ExceptionMessagesContainRawData = false,
            MissingFieldFound = null,
        };
        try
        {
            using var csv = new CsvReader(text, configuration);
            csv.Read();
            csv.ReadHeader();
            var headers = csv.HeaderRecord ?? throw new InvalidDataException($"{source} has no header row.");
            var missing = RequiredColumns.Except(headers).ToList();
            if (missing.Count > 0) throw new InvalidDataException($"{source} is missing columns: {string.Join(", ", missing)}.");
            while (csv.Read())
                rows.Add((csv.Parser.Row, headers.ToDictionary(h => h, h => (csv.GetField(h) ?? "").Trim())));
        }
        catch (CsvHelperException ex)
        {
            throw new InvalidDataException($"{source}: row {ex.Context?.Parser?.Row} could not be read as CSV.");
        }

        var issues = new List<IngestionIssue>();
        var claims = new List<ClaimRecord>();
        foreach (var group in rows.GroupBy(r => r.Fields["claim_id"]))
        {
            var first = group.First().Fields;
            foreach (var field in ClaimLevelFields)
                if (group.Any(r => r.Fields[field] != first[field]))
                    issues.Add(new(IssueKinds.ClaimsHeaderConflict, Severity.Warning, source, $"{group.Key}:{field}",
                        $"Lines of {group.Key} disagree on '{field}'; the first line's value is used.", group.Key));

            var dateOfBirth = ParseDate(first["patient_dob"]);
            var dateOfService = ParseDate(first["dos"]);
            var submitted = ParseDate(first["submitted_date"]);
            if (dateOfBirth is null || dateOfService is null || submitted is null)
            {
                issues.Add(new(IssueKinds.ClaimsRowInvalid, Severity.Error, source, group.Key,
                    "Claim has an invalid date of birth, service date or submission date; claim skipped.", group.Key));
                continue;
            }

            var lines = new List<ClaimLine>();
            foreach (var (rowNumber, fields) in group)
            {
                if (!int.TryParse(fields["line_no"], NumberStyles.Integer, CultureInfo.InvariantCulture, out var lineNo)
                    || !decimal.TryParse(fields["units"], NumberStyles.Number, CultureInfo.InvariantCulture, out var units)
                    || !decimal.TryParse(fields["charge"], NumberStyles.Number, CultureInfo.InvariantCulture, out var charge))
                {
                    issues.Add(new(IssueKinds.ClaimsRowInvalid, Severity.Error, source, $"row {rowNumber}",
                        $"A line of {group.Key} has an invalid line number, units or charge; line skipped.", group.Key));
                    continue;
                }
                if (lines.Any(l => l.LineNo == lineNo))
                {
                    issues.Add(new(IssueKinds.ClaimsDuplicateLine, Severity.Warning, source, $"{group.Key}:{lineNo}",
                        $"Line {lineNo} of {group.Key} appears more than once; the first is used.", group.Key));
                    continue;
                }
                var diagnosisCodes = new[] { fields["dx1"], fields["dx2"], fields["dx3"], fields["dx4"] }.Where(d => d.Length > 0).ToList();
                lines.Add(new ClaimLine(lineNo, fields["cpt"], fields["modifier"], units, charge, diagnosisCodes, fields["auth_number"]));
            }
            if (lines.Count == 0) continue;

            claims.Add(new ClaimRecord(group.Key, first["patient_first"], first["patient_last"], dateOfBirth.Value, first["member_id"],
                first["payer"], first["payer_id"], dateOfService.Value, submitted.Value, first["rendering_npi"], first["rendering_provider"],
                first["facility"], first["pos"], first["coder_id"],
                first["prebill_reviewed"].Equals("Y", StringComparison.OrdinalIgnoreCase), lines));
        }
        return (claims, issues);
    }

    private static DateOnly? ParseDate(string text) =>
        DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? date : null;
}
