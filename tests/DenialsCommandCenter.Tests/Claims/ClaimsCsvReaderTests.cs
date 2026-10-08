using DenialsCommandCenter.Domain.Claims;
using DenialsCommandCenter.Domain.Ingestion;

namespace DenialsCommandCenter.Tests.Claims;

public class ClaimsCsvReaderTests
{
    private const string Header =
        "claim_id,patient_first,patient_last,patient_dob,member_id,payer,payer_id,dos,submitted_date,rendering_npi,rendering_provider,facility,pos,line_no,cpt,modifier,units,charge,dx1,dx2,dx3,dx4,auth_number,coder_id,prebill_reviewed";

    private static (IReadOnlyList<ClaimRecord> Claims, IReadOnlyList<IngestionIssue> Issues) Read(params string[] rows) =>
        ClaimsCsvReader.Read(new StringReader(string.Join('\n', new[] { Header }.Concat(rows))));

    [Fact]
    public void Groups_lines_into_claims()
    {
        var (claims, issues) = Read(
            "GPP-2026-000001,Jane,Doe,1940-01-02,M1,Northstar Health Plan,NS401,2026-02-01,2026-02-03,111,Dr A,St. Anselm Hospital,21,1,99232,25,1,140.00,I10,E11.9,,,,C07,N",
            "GPP-2026-000001,Jane,Doe,1940-01-02,M1,Northstar Health Plan,NS401,2026-02-01,2026-02-03,111,Dr A,St. Anselm Hospital,21,2,31500,,1,260.00,I10,,,,,C07,N");

        Assert.Empty(issues);
        var claim = Assert.Single(claims);
        Assert.Equal(400.00m, claim.TotalCharge);
        Assert.False(claim.PrebillReviewed);
        Assert.Equal(new DateOnly(2026, 2, 1), claim.DateOfService);
        Assert.Equal(new[] { "I10", "E11.9" }, claim.Lines[0].DiagnosisCodes);
        Assert.Equal("25", claim.Lines[0].Modifier);
    }

    [Fact]
    public void Reports_header_conflicts_duplicate_lines_and_invalid_rows()
    {
        var (claims, issues) = Read(
            "GPP-2026-000002,Jane,Doe,1940-01-02,M1,Northstar Health Plan,NS401,2026-02-01,2026-02-03,111,Dr A,St. Anselm Hospital,21,1,99232,,1,140.00,I10,,,,,C07,Y",
            "GPP-2026-000002,Jane,Doe,1940-01-02,M1,Meridian PPO,NS401,2026-02-01,2026-02-03,111,Dr A,St. Anselm Hospital,21,1,99232,,1,140.00,I10,,,,,C07,Y",
            "GPP-2026-000002,Jane,Doe,1940-01-02,M1,Northstar Health Plan,NS401,2026-02-01,2026-02-03,111,Dr A,St. Anselm Hospital,21,2,31500,,1,abc,I10,,,,,C07,Y");

        Assert.Single(claims);
        Assert.Single(claims[0].Lines);
        Assert.Contains(issues, i => i.Kind == IssueKinds.ClaimsHeaderConflict && i.Reference == "GPP-2026-000002:payer");
        Assert.Contains(issues, i => i.Kind == IssueKinds.ClaimsDuplicateLine);
        Assert.Contains(issues, i => i.Kind == IssueKinds.ClaimsRowInvalid);
    }

    [Fact]
    public void Short_row_becomes_an_issue_not_an_exception()
    {
        var (claims, issues) = Read(
            "GPP-2026-000001,Jane,Doe,1940-01-02,M1,Northstar Health Plan,NS401,2026-02-01,2026-02-03,111,Dr A,St. Anselm Hospital,21,1,99232,,1,140.00,I10,,,,,C07,N",
            "GPP-2026-000003,Zelda,Quill");

        Assert.Single(claims);
        Assert.Contains(issues, i => i.Kind == IssueKinds.ClaimsRowInvalid && i.ClaimId == "GPP-2026-000003");
        Assert.DoesNotContain(issues, i => i.Reason.Contains("Zelda") || i.Reason.Contains("Quill"));
    }

    [Fact]
    public void Malformed_csv_error_does_not_echo_row_content()
    {
        var error = Assert.Throws<InvalidDataException>(() => Read(
            "GPP-2026-000004,Zel\"da,Quill,1940-01-02,M1,Northstar Health Plan,NS401,2026-02-01,2026-02-03,111,Dr A,St. Anselm Hospital,21,1,99232,,1,140.00,I10,,,,,C07,N"));

        Assert.Equal("claims_export.csv: row 2 could not be read as CSV.", error.Message);
    }

    [Fact]
    public void Reads_the_real_export()
    {
        using var reader = new StreamReader(TestData.PathOf("claims_export.csv"));
        var (claims, issues) = ClaimsCsvReader.Read(reader);

        Assert.Equal(1222, claims.Count);
        Assert.Equal(1323, claims.Sum(c => c.Lines.Count));
        Assert.Empty(issues);
    }
}
