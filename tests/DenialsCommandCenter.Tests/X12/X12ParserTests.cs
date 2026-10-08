using DenialsCommandCenter.Domain.X12;

namespace DenialsCommandCenter.Tests.X12;

public class X12ParserTests
{
    [Fact]
    public void Sample_isa_is_fixed_width() => Assert.Equal(105, X12Samples.Basic.IndexOf('~'));

    [Fact]
    public void Parses_transaction_claim_services_adjustments_and_remarks()
    {
        var remittance = X12Parser.Parse(X12Samples.Basic);

        Assert.Equal("100000201", remittance.InterchangeControlNumber);
        Assert.Equal(new DateOnly(2026, 4, 15), remittance.InterchangeDate);
        var transaction = Assert.Single(remittance.Transactions);
        Assert.Equal("COASTAL SENIOR ADVANTAGE", transaction.PayerName);
        Assert.Equal("CSA77", transaction.PayerId);
        Assert.Equal("TRACE001", transaction.TraceNumber);
        Assert.Equal(250.00m, transaction.PaymentAmount);
        Assert.Equal(new DateOnly(2026, 3, 12), transaction.PaymentDate);

        var claim = Assert.Single(transaction.Claims);
        Assert.Equal("GPP2026000494", claim.RawClaimRef);
        Assert.Equal("1", claim.StatusCode);
        Assert.Equal(400.00m, claim.Charge);
        Assert.Equal(250.00m, claim.Paid);
        Assert.Equal(0m, claim.PatientResponsibility);
        Assert.Equal("ICN0001", claim.PayerClaimControlNumber);
        Assert.Equal("1", claim.FrequencyCode);
        Assert.Equal("PATEL", claim.PatientLastName);
        Assert.Equal("CS54140769", claim.MemberId);
        Assert.Equal("1262252103", claim.RenderingNpi);
        Assert.Equal(new DateOnly(2026, 1, 28), claim.ReceivedDate);
        Assert.Empty(claim.Adjustments);

        Assert.Equal(2, claim.Services.Count);
        var evaluationService = claim.Services[0];
        Assert.Equal("99232", evaluationService.ProcedureCode);
        Assert.Equal(new[] { "25" }, evaluationService.Modifiers);
        Assert.Equal(new DateOnly(2026, 1, 10), evaluationService.ServiceDate);
        Assert.Equal(new Adjustment("CO", "97", 140.00m), Assert.Single(evaluationService.Adjustments));
        Assert.Equal(new[] { "M15" }, evaluationService.Remarks);
        Assert.Empty(claim.Services[1].Modifiers);
        Assert.Equal(250.00m, claim.Services[1].Paid);
    }

    [Fact]
    public void Reads_delimiters_from_the_isa_segment()
    {
        var custom = X12Samples.Basic.Replace('*', '|').Replace(':', '>').Replace('~', '\n');

        var claim = X12Parser.Parse(custom).Transactions[0].Claims[0];

        Assert.Equal(new[] { "25" }, claim.Services[0].Modifiers);
        Assert.Equal(250.00m, claim.Paid);
    }

    [Fact]
    public void Parses_provider_level_adjustments()
    {
        var withPlb = X12Samples.Basic.Replace("SE*20*0001~", "PLB*1932145067*20261231*WO:GPP2026000494*161.20~\nSE*21*0001~");

        var transaction = X12Parser.Parse(withPlb).Transactions[0];

        Assert.Equal(new ProviderAdjustment("WO", "GPP2026000494", 161.20m), Assert.Single(transaction.ProviderAdjustments));
        Assert.Single(transaction.Claims);
    }

    [Fact]
    public void Rejects_content_that_is_not_an_835() =>
        Assert.Throws<X12FormatException>(() => X12Parser.Parse("hello, this is not an 835"));

    [Fact]
    public void Rejects_invalid_amounts() =>
        Assert.Throws<X12FormatException>(() => X12Parser.Parse(X12Samples.Basic.Replace("*400.00*250.00*", "*4X0.00*250.00*")));

    [Fact]
    public void Rejects_missing_se_trailer() =>
        Assert.Throws<X12FormatException>(() => X12Parser.Parse(X12Samples.Basic.Replace("SE*20*0001~", "")));

    [Fact]
    public void Parses_the_real_q1_file()
    {
        var remittance = X12Parser.Parse(File.ReadAllText(TestData.PathOf("remits", "era_2026Q1.835")));

        Assert.Equal(7, remittance.Transactions.Count);
        Assert.Equal(346, remittance.Transactions.Sum(t => t.Claims.Count));
        Assert.Equal(38012.20m, remittance.Transactions.Sum(t => t.PaymentAmount));
    }
}
