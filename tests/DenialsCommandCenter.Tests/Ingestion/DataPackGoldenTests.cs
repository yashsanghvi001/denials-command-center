using System.Text.Json;
using DenialsCommandCenter.Api.Ingestion;
using DenialsCommandCenter.Domain;
using DenialsCommandCenter.Domain.Claims;
using DenialsCommandCenter.Domain.Ingestion;

namespace DenialsCommandCenter.Tests.Ingestion;

public class DataPackGoldenTests
{
    private static readonly DateOnly Today = new(2026, 9, 30);
    private static readonly Lazy<PipelineInput> Input = new(() => DataPackLoader.Load(TestData.Dir, Today));
    private static readonly Lazy<PipelineResult> CachedResult = new(() => IngestionPipeline.Run(Input.Value));
    private static PipelineResult Result => CachedResult.Value;
    private static ClaimState State(string id) => Result.States.Single(s => s.ClaimId == id);

    [Fact]
    public void Claims_and_lines() => Assert.Equal((1222, 1323), (Result.Claims.Count, Result.Claims.Sum(c => c.Lines.Count)));

    [Fact]
    public void Resent_q2_file_is_fully_deduplicated()
    {
        var file = Result.Reconciliation.Files.Single(x => x.FileName == "era_2026Q2_resent_0719.835");
        Assert.Equal((453, 0, 453), (file.ClaimPayments, file.NewEvents, file.DuplicateEvents));
        Assert.Contains(Result.Issues, i => i.Kind == IssueKinds.DuplicateRemittanceFile && i.Source == file.FileName);
    }

    [Fact]
    public void Unique_events_and_reversals()
    {
        Assert.Equal(1197, Result.Events.Count);
        var reversals = Result.Events.Where(e => e.Payment.StatusCode == "22").ToList();
        Assert.Equal(11, reversals.Count);
        Assert.Equal(-1379.50m, reversals.Sum(e => e.Payment.Paid));
        Assert.DoesNotContain(Result.Issues, i => i.Kind == IssueKinds.UnmatchedReversal);
    }

    [Fact]
    public void Reconciliation_balances_to_the_cent()
    {
        var reconciliation = Result.Reconciliation;
        Assert.Equal(165681.98m, reconciliation.PayerFilesTotalIncludingDuplicates);
        Assert.Equal(47461.62m, reconciliation.DuplicatePaymentsExcluded);
        Assert.Equal(118220.36m, reconciliation.PayerFilesTotal);
        Assert.Equal(117959.96m, reconciliation.PaidToKnownClaims);
        Assert.Equal(260.40m, reconciliation.PaidToUnmatched);
        Assert.Equal(0m, reconciliation.Difference);
    }

    [Fact]
    public void Other_practice_payments_are_exceptions()
    {
        var unmatched = Result.Issues.Where(i => i.Kind == IssueKinds.UnmatchedRemittance).ToList();
        Assert.Equal(3, unmatched.Count);
        Assert.Equal(260.40m, unmatched.Sum(i => i.Amount));
        Assert.All(unmatched, i => Assert.Contains("BHC-2026-", i.Reason));
    }

    [Fact]
    public void Claim_status_counts()
    {
        var counts = Result.Reconciliation.ClaimsByStatus;
        Assert.Equal((1009, 136, 19, 58), (counts["Paid"], counts["Denied"], counts["PartiallyDenied"], counts["NoResponse"]));
        Assert.Equal((0, 0), (counts["Reversed"], counts["Unknown"]));
    }

    [Fact]
    public void Denied_amount_total() =>
        Assert.Equal(31145.00m, Result.States.Where(s => s.Status is ClaimStatus.Denied or ClaimStatus.PartiallyDenied).Sum(s => s.DeniedAmount));

    [Fact]
    public void Overpayments_are_flagged()
    {
        var overpayments = Result.Issues.Where(i => i.Kind == IssueKinds.PossibleOverpayment).OrderBy(i => i.ClaimId).Select(i => (i.ClaimId, i.Amount)).ToList();
        var expected = new (string?, decimal?)[] { ("GPP-2026-001077", 161.20m), ("GPP-2026-001730", 161.20m) };
        Assert.Equal(expected, overpayments);
    }

    [Fact]
    public void Payer_recoupment_example()
    {
        var state = State("GPP-2026-000230");
        Assert.Equal(ClaimStatus.Denied, state.Status);
        Assert.True(state.WasRecouped);
        Assert.Equal(0m, state.NetPaid);
        Assert.Equal(new DateOnly(2026, 8, 26), state.DenialDate);
        Assert.Equal("197", Assert.Single(state.DenialLines).Reason);
    }

    [Fact]
    public void Worklog_injection_is_flagged_and_not_obeyed()
    {
        Assert.Contains(Result.Worklog, w => w.ClaimId == "GPP-2026-001846" && w.SuspiciousText);
        Assert.Equal(ClaimStatus.Denied, State("GPP-2026-001846").Status);
    }

    [Fact]
    public void Run_is_deterministic()
    {
        var again = IngestionPipeline.Run(Input.Value);
        Assert.Equal(JsonSerializer.Serialize(Result, JsonDefaults.Options), JsonSerializer.Serialize(again, JsonDefaults.Options));
    }

    [Fact]
    public void File_names_do_not_change_results()
    {
        var renamed = Input.Value with
        {
            Remits = Input.Value.Remits.Select(r => r with { FileName = r.FileName.Contains("Q1") ? "z_" + r.FileName : "a_" + r.FileName }).ToList(),
        };
        var other = IngestionPipeline.Run(renamed);

        Assert.Equal(JsonSerializer.Serialize(Result.States, JsonDefaults.Options), JsonSerializer.Serialize(other.States, JsonDefaults.Options));
        Assert.Equal(Result.Reconciliation.PayerFilesTotal, other.Reconciliation.PayerFilesTotal);
    }

    [Fact]
    public void A_new_remittance_file_adds_only_its_payments()
    {
        var withNew = Input.Value with
        {
            Remits = [.. Input.Value.Remits, new RemitSource("era_live.835", System.Text.Encoding.UTF8.GetBytes(X12.X12Samples.Basic))],
        };
        var extended = IngestionPipeline.Run(withNew);

        Assert.Equal(1198, extended.Events.Count);
        Assert.Equal(118470.36m, extended.Reconciliation.PayerFilesTotal);
        Assert.Equal(0m, extended.Reconciliation.Difference);
    }
}
