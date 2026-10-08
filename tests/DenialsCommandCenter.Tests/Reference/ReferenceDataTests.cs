using DenialsCommandCenter.Api.Analysis;
using DenialsCommandCenter.Domain.Reference;

namespace DenialsCommandCenter.Tests.Reference;

public class ReferenceDataTests
{
    private static PolicyLibrary Policies() => PolicyLibrary.Parse(
        Directory.GetFiles(TestData.PathOf("payer_policies"), "*.md").Select(p => (Path.GetFileName(p), File.ReadAllText(p))));

    [Fact]
    public void Reads_payer_rules_by_payer_id()
    {
        using var reader = new StreamReader(TestData.PathOf("payer_rules.csv"));
        var rules = PayerRulesReader.Read(reader);

        Assert.Equal(4, rules.Count);
        Assert.Equal(new PayerRule("SMP12", "Sunshine Medicaid Partners", 120, 60, 60), rules["SMP12"]);
        Assert.Equal(90, rules["mrd55"].AppealWindowDays);
    }

    [Fact]
    public void Reads_reason_code_descriptions()
    {
        using var reader = new StreamReader(TestData.PathOf("carc_rarc_reference.csv"));
        var codes = ReasonCodeReader.Read(reader);

        Assert.Equal("Precertification/authorization/notification/pre-treatment absent.", codes.DescribeCarc("197"));
        Assert.Equal("Missing/incomplete/invalid credentialing data.", codes.Rarc["N570"]);
        Assert.Equal("Unknown reason code", codes.DescribeCarc("999"));
    }

    [Fact]
    public void Parses_every_policy_into_numbered_sections()
    {
        var policies = Policies();

        Assert.Equal(
            new[] { "ALL_PAYERS_MOD25-2026", "CSA_PROVIDER-ENROLLMENT", "MPPO_DX-EXCL-03", "NSHP_HOSP-FREQ-07", "SMP_SNF-AUTH-2026" },
            policies.Documents.Select(d => d.PolicyId));
        Assert.Equal(new[] { 3, 4, 4, 4, 5 }, policies.Documents.Select(d => d.Sections.Count));
        Assert.StartsWith("Claims for dates of service before 2026-04-01", policies.Find("SMP_SNF-AUTH-2026 §3")!.Text);
        Assert.Contains("180 days", policies.Find("NSHP_HOSP-FREQ-07 §4")!.Text);
        Assert.Contains("MPPO_DX-EXCL-03 §2", policies.Citations);
        Assert.Null(policies.Find("MPPO_DX-EXCL-03 §9"));
    }

    [Fact]
    public void Continuation_lines_join_their_section()
    {
        var document = PolicyLibrary.ParseDocument("TEST-1", "# Title\nIntro line\n1. First part\ncontinues here\n\n2. Second");

        Assert.Equal("Title", document.Title);
        Assert.Equal("First part continues here", document.Sections[0].Text);
        Assert.Equal("TEST-1 §2", document.Sections[1].Citation);
    }

    [Fact]
    public void Repeated_section_numbers_keep_the_first_section()
    {
        var document = PolicyLibrary.ParseDocument("TEST-2", "1. a\n2. b\n## Next\n1. c");

        var library = new PolicyLibrary([document]);

        Assert.Equal("a", library.Find("TEST-2 §1")!.Text);
        Assert.Equal(new[] { "TEST-2 §1", "TEST-2 §2" }, library.Citations.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Payer_rules_load_without_the_policy_folder()
    {
        var dataDir = Directory.CreateTempSubdirectory().FullName;
        try
        {
            File.Copy(TestData.PathOf("payer_rules.csv"), Path.Combine(dataDir, "payer_rules.csv"));

            Assert.Equal(4, ReferenceDataLoader.LoadPayerRules(dataDir).Count);
        }
        finally
        {
            Directory.Delete(dataDir, recursive: true);
        }
    }

    [Fact]
    public void Reads_the_forty_labels()
    {
        using var reader = new StreamReader(TestData.PathOf("labeled_denials_sample.csv"));
        var labels = LabeledDenialsReader.Read(reader);

        Assert.Equal(40, labels.Count);
        Assert.Equal(new LabeledDenial("GPP-2026-000230", "Payer error", "Denials (appeal)", "No"), labels[0]);
    }
}
