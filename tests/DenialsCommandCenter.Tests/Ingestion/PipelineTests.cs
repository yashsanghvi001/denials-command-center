using System.Text;
using DenialsCommandCenter.Domain.Claims;
using DenialsCommandCenter.Domain.Ingestion;
using DenialsCommandCenter.Tests.X12;

namespace DenialsCommandCenter.Tests.Ingestion;

public class PipelineTests
{
    private const string Claims = """
claim_id,patient_first,patient_last,patient_dob,member_id,payer,payer_id,dos,submitted_date,rendering_npi,rendering_provider,facility,pos,line_no,cpt,modifier,units,charge,dx1,dx2,dx3,dx4,auth_number,coder_id,prebill_reviewed
GPP-2026-000494,Noah,Patel,1950-01-01,CS54140769,Coastal Senior Advantage,CSA77,2026-01-10,2026-01-20,1262252103,Samuel Pierce,St. Anselm Hospital,21,1,99232,25,1,140.00,I10,,,,,C01,Y
GPP-2026-000494,Noah,Patel,1950-01-01,CS54140769,Coastal Senior Advantage,CSA77,2026-01-10,2026-01-20,1262252103,Samuel Pierce,St. Anselm Hospital,21,2,31500,,1,260.00,I10,,,,,C01,Y
""";

    private static RemitSource Remit(string name, string content) => new(name, Encoding.UTF8.GetBytes(content));

    private static PipelineResult Run(params RemitSource[] remits) =>
        IngestionPipeline.Run(new PipelineInput(Claims, remits, [], new DateOnly(2026, 9, 30)));

    [Fact]
    public void Single_remit_balances_and_projects_partial_denial()
    {
        var result = Run(Remit("a.835", X12Samples.Basic));

        Assert.Equal(0m, result.Reconciliation.Difference);
        var state = Assert.Single(result.States);
        Assert.Equal(ClaimStatus.PartiallyDenied, state.Status);
        Assert.Equal(140m, state.DeniedAmount);
    }

    [Fact]
    public void Provider_level_adjustment_reduces_the_payment_and_still_reconciles()
    {
        var withPlb = X12Samples.Basic
            .Replace("BPR*I*250.00", "BPR*I*200.00")
            .Replace("SE*20*0001~", "PLB*1932145067*20261231*WO:GPP2026000494*50.00~\nSE*21*0001~");

        var result = Run(Remit("a.835", withPlb));

        Assert.Equal(50m, result.Reconciliation.ProviderLevelAdjustments);
        Assert.Equal(200m, result.Reconciliation.PayerFilesTotal);
        Assert.Equal(0m, result.Reconciliation.Difference);
        Assert.DoesNotContain(result.Issues, i => i.Kind == IssueKinds.TransactionOutOfBalance);
    }

    [Fact]
    public void Partial_resend_adds_only_new_payments_and_is_flagged()
    {
        var extraClaim = "CLP*GPP2026000495*1*50.00*50.00**12*ICN0002*11*1~\nNM1*QC*1*DOE*JANE****MI*X1~\n";
        var partial = X12Samples.Basic.Replace("BPR*I*250.00", "BPR*I*300.00").Replace("SE*20*0001~", extraClaim + "SE*22*0001~");

        var result = Run(Remit("a_original.835", X12Samples.Basic), Remit("b_partial_resend.835", partial));

        Assert.Equal(2, result.Events.Count);                                  // original + the one genuinely new payment
        Assert.Contains(result.Issues, i => i.Kind == IssueKinds.PartialDuplicateTransaction);
        Assert.Equal(250m, result.Reconciliation.Difference);                  // surfaced, not hidden
        var file = result.Reconciliation.Files.Single(f => f.FileName == "b_partial_resend.835");
        Assert.Equal((2, 1, 1), (file.ClaimPayments, file.NewEvents, file.DuplicateEvents));
    }

    [Fact]
    public void Malformed_file_becomes_an_exception_not_a_crash()
    {
        var result = Run(Remit("a.835", X12Samples.Basic), Remit("broken.835", "this is not an 835"));

        Assert.Contains(result.Issues, i => i.Kind == IssueKinds.UnparseableFile && i.Source == "broken.835");
        Assert.Single(result.Events);
        Assert.Contains(result.Reconciliation.Files, f => f.FileName == "broken.835" && f.Outcome.StartsWith("Rejected"));
    }

    [Fact]
    public void Resent_file_reports_the_amount_it_excluded()
    {
        var resent = X12Samples.Basic.Replace("*100000201*", "*100000202*");

        var result = Run(Remit("a.835", X12Samples.Basic), Remit("b_resent.835", resent));

        var issue = Assert.Single(result.Issues, i => i.Kind == IssueKinds.DuplicateRemittanceFile);
        Assert.EndsWith("$250.00 was not counted again.", issue.Reason);
        Assert.Equal(0m, result.Reconciliation.Difference);
    }

    [Fact]
    public void Resent_payments_under_a_new_payment_total_say_what_was_counted()
    {
        var newTotal = X12Samples.Basic.Replace("BPR*I*250.00", "BPR*I*260.00");

        var result = Run(Remit("a.835", X12Samples.Basic), Remit("b_new_total.835", newTotal));

        var issue = Assert.Single(result.Issues, i => i.Kind == IssueKinds.DuplicateRemittanceFile);
        Assert.Contains("$0.00 excluded, but $260.00", issue.Reason);
        Assert.DoesNotContain("not counted again", issue.Reason);
        Assert.Equal(260m, result.Reconciliation.Difference);
    }

    [Fact]
    public void Two_unmatched_payments_with_the_same_claim_reference_are_two_exceptions()
    {
        var secondPayment = "CLP*BHC-2026-456493*1*50.00*50.00**12*ICN0002*11*1~\nNM1*QC*1*DOE*JANE****MI*X1~\n";
        var remit = X12Samples.Basic
            .Replace("CLP*GPP2026000494*", "CLP*BHC-2026-456493*")
            .Replace("BPR*I*250.00", "BPR*I*300.00")
            .Replace("SE*20*0001~", secondPayment + "SE*22*0001~");

        var result = Run(Remit("a.835", remit));

        var unmatched = result.Issues.Where(i => i.Kind == IssueKinds.UnmatchedRemittance).ToList();
        Assert.Equal(2, unmatched.Count);
        Assert.Equal(300m, unmatched.Sum(i => i.Amount));
        Assert.Equal(0m, result.Reconciliation.Difference);
    }

    [Fact]
    public void Byte_identical_file_is_skipped()
    {
        var result = Run(Remit("a.835", X12Samples.Basic), Remit("copy.835", X12Samples.Basic));

        Assert.Single(result.Events);
        Assert.Contains(result.Issues, i => i.Kind == IssueKinds.DuplicateFile && i.Source == "copy.835");
    }

    [Fact]
    public void Reversal_with_no_matching_payment_is_an_exception()
    {
        var orphanReversal = X12Samples.Basic.Replace("*1*400.00*250.00*0*12*ICN0001*", "*22*400.00*250.00*0*12*ICN0999*");

        var result = Run(Remit("a.835", orphanReversal));

        var issue = Assert.Single(result.Issues, i => i.Kind == IssueKinds.UnmatchedReversal);
        Assert.Equal("GPP-2026-000494", issue.ClaimId);
        Assert.Equal(Severity.Warning, issue.Severity);
    }

    [Fact]
    public void Member_mismatch_is_not_applied_to_the_claim()
    {
        var result = Run(Remit("a.835", X12Samples.Basic.Replace("MI*CS54140769", "MI*CS00000000")));

        Assert.Null(Assert.Single(result.Events).ClaimId);
        Assert.Contains(result.Issues, i => i.Kind == IssueKinds.MemberMismatch && i.ClaimId == "GPP-2026-000494");
        Assert.Equal(ClaimStatus.NoResponse, result.States[0].Status);
        Assert.Equal(0m, result.Reconciliation.Difference);
    }
}
