using System.Globalization;
using DenialsCommandCenter.Domain.Claims;
using DenialsCommandCenter.Domain.X12;

namespace DenialsCommandCenter.Tests.Claims;

public class ClaimStateProjectorTests
{
    private static RemitEvent Event(string status, decimal paid, string date, int sequence, string icn = "ICN1", params Adjustment[] serviceAdjustments)
    {
        var service = new ServicePayment("99232", [], 0m, paid, 1m, null, serviceAdjustments, ["N54"]);
        var payment = new ClaimPayment(sequence, "GPP2026000001", status, 0m, paid, 0m, icn, "1", "DOE", "JANE", "M1", "1", null, [], [], [service]);
        return new RemitEvent($"k{sequence}", "GPP-2026-000001", "SMP12", "SMP", $"T{sequence}",
            DateOnly.ParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture), "f.835", 0, payment);
    }

    private static ClaimState Project(params RemitEvent[] events) => ClaimStateProjector.Project("GPP-2026-000001", 170m, events);

    [Fact]
    public void No_events_means_no_response()
    {
        var state = Project();
        Assert.Equal(ClaimStatus.NoResponse, state.Status);
        Assert.Equal(0, state.EventCount);
    }

    [Fact]
    public void Contractual_adjustment_only_is_paid_with_no_denial()
    {
        var state = Project(Event("1", 105.40m, "2026-03-21", 1, "ICN1", new Adjustment("CO", "45", 64.60m)));
        Assert.Equal(ClaimStatus.Paid, state.Status);
        Assert.Equal(0m, state.DeniedAmount);
        Assert.Null(state.DenialDate);
        Assert.Equal(105.40m, state.NetPaid);
    }

    [Fact]
    public void Status_4_is_denied_on_the_remittance_payment_date()
    {
        var state = Project(Event("4", 0m, "2026-05-20", 1, "ICN1", new Adjustment("CO", "197", 170m)));
        Assert.Equal(ClaimStatus.Denied, state.Status);
        Assert.Equal(170m, state.DeniedAmount);
        Assert.Equal(new DateOnly(2026, 5, 20), state.DenialDate);
        var line = Assert.Single(state.DenialLines);
        Assert.Equal(("CO", "197"), (line.Group, line.Reason));
        Assert.Equal(new[] { "N54" }, line.Remarks);
    }

    [Fact]
    public void Patient_responsibility_is_not_a_denial()
    {
        var state = Project(Event("1", 80m, "2026-03-21", 1, "ICN1", new Adjustment("CO", "45", 70m), new Adjustment("PR", "2", 20m)));
        Assert.Equal(ClaimStatus.Paid, state.Status);
    }

    [Fact]
    public void Paid_claim_with_a_bundled_line_is_partially_denied()
    {
        var state = Project(Event("1", 161.20m, "2026-03-07", 1, "ICN1", new Adjustment("CO", "97", 140m), new Adjustment("CO", "45", 98.80m)));
        Assert.Equal(ClaimStatus.PartiallyDenied, state.Status);
        Assert.Equal(140m, state.DeniedAmount);
        Assert.Equal(new DateOnly(2026, 3, 7), state.DenialDate);
    }

    [Fact]
    public void Payer_reversal_then_denial_on_the_same_day_ends_denied_and_recouped()
    {
        // The denial has a LOWER segment sequence than the reversal, so ordering must put reversals first by status.
        var state = Project(
            Event("1", 148.80m, "2026-03-29", 1),
            Event("4", 0m, "2026-08-26", 2, "ICN1", new Adjustment("CO", "197", 240m)),
            Event("22", -148.80m, "2026-08-26", 3));
        Assert.Equal(ClaimStatus.Denied, state.Status);
        Assert.True(state.WasRecouped);
        Assert.Equal(0m, state.NetPaid);
        Assert.Equal(240m, state.DeniedAmount);
        Assert.Equal(new DateOnly(2026, 8, 26), state.DenialDate);
        Assert.Equal(0m, state.PossibleOverpayment);
    }

    [Fact]
    public void Reversal_remitted_on_the_same_day_as_its_original_still_cancels_it()
    {
        var state = Project(
            Event("22", -100m, "2026-05-01", 1),
            Event("1", 100m, "2026-05-01", 2));
        Assert.Equal(ClaimStatus.Reversed, state.Status);
        Assert.True(state.WasRecouped);
        Assert.Equal(0, state.UnmatchedReversals);
        Assert.Equal(0m, state.NetPaid);
    }

    [Fact]
    public void Reversal_matching_no_payment_is_counted_and_is_not_a_recoupment()
    {
        var state = Project(
            Event("1", 100m, "2026-03-01", 1, "ICN1"),
            Event("22", -100m, "2026-08-01", 2, "ICN9"));
        Assert.Equal(ClaimStatus.Paid, state.Status);
        Assert.False(state.WasRecouped);
        Assert.Equal(1, state.UnmatchedReversals);
    }

    [Fact]
    public void Corrected_claim_paid_without_reversing_the_original_is_a_possible_overpayment()
    {
        var state = Project(Event("1", 161.20m, "2026-03-07", 1), Event("1", 248.00m, "2026-04-14", 2));
        Assert.Equal(ClaimStatus.Paid, state.Status);
        Assert.Equal(409.20m, state.NetPaid);
        Assert.Equal(161.20m, state.PossibleOverpayment);
    }

    [Fact]
    public void Denial_then_corrected_claim_paid_ends_paid_with_no_overpayment()
    {
        var state = Project(Event("4", 0m, "2026-03-02", 1, "ICN1", new Adjustment("CO", "11", 330m)), Event("1", 204.60m, "2026-04-27", 2));
        Assert.Equal(ClaimStatus.Paid, state.Status);
        Assert.Equal(0m, state.PossibleOverpayment);
        Assert.Equal(0m, state.DeniedAmount);
    }

    [Fact]
    public void Reversal_with_nothing_after_is_reversed()
    {
        var state = Project(Event("1", 100m, "2026-03-01", 1), Event("22", -100m, "2026-08-01", 2));
        Assert.Equal(ClaimStatus.Reversed, state.Status);
        Assert.Equal(0m, state.NetPaid);
    }

    [Fact]
    public void Unrecognized_status_code_is_unknown() => Assert.Equal(ClaimStatus.Unknown, Project(Event("23", 0m, "2026-03-01", 1)).Status);
}
