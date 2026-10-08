using DenialsCommandCenter.Domain.Ingestion;
using DenialsCommandCenter.Domain.X12;
using DenialsCommandCenter.Tests.X12;

namespace DenialsCommandCenter.Tests.Ingestion;

public class EventKeyTests
{
    private static readonly RemittanceTransaction Transaction = X12Parser.Parse(X12Samples.Basic).Transactions[0];

    [Fact]
    public void Same_payment_gives_same_key_regardless_of_amount_scale()
    {
        var claim = Transaction.Claims[0];
        var rescaled = claim with { Paid = 250m, Charge = 400.0m };
        Assert.Equal(EventKey.For("CSA77", "TRACE001", claim), EventKey.For("CSA77", "TRACE001", rescaled));
    }

    [Fact]
    public void Different_trace_number_gives_different_key()
    {
        var claim = Transaction.Claims[0];
        Assert.NotEqual(EventKey.For("CSA77", "TRACE001", claim), EventKey.For("CSA77", "TRACE002", claim));
    }

    [Fact]
    public void Reversal_and_original_have_different_keys()
    {
        var claim = Transaction.Claims[0];
        var reversal = claim with { StatusCode = "22", Paid = -250m, Charge = -400m };
        Assert.NotEqual(EventKey.For("CSA77", "T", claim), EventKey.For("CSA77", "T", reversal));
    }

    [Fact]
    public void Issue_key_depends_only_on_kind_source_and_reference()
    {
        var issue = new IngestionIssue(IssueKinds.UnmatchedRemittance, Severity.Error, "f.835", "T/BHC/1", "reason one");
        var reworded = issue with { Reason = "reason two", Amount = 5m };
        Assert.Equal(issue.Key, reworded.Key);
        Assert.Equal(64, issue.Key.Length);
    }
}
