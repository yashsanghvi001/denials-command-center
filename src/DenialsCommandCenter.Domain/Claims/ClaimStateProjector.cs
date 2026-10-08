using DenialsCommandCenter.Domain.X12;

namespace DenialsCommandCenter.Domain.Claims;

public static class ClaimStateProjector
{
    public const string ReversalStatus = "22";
    public const string DeniedStatus = "4";
    private static readonly HashSet<string> PaidStatuses = ["1", "2", "3", "19", "20", "21"];
    private static readonly HashSet<string> ContractualReasons = ["45"];

    public static IReadOnlyList<RemitEvent> Order(IEnumerable<RemitEvent> events) =>
        events.OrderBy(e => e.PaymentDate)
              .ThenBy(e => e.Payment.StatusCode == ReversalStatus ? 0 : 1)
              .ThenBy(e => e.FileOrder)
              .ThenBy(e => e.Payment.Sequence)
              .ToList();

    public static ClaimState Project(string claimId, decimal billedCharge, IEnumerable<RemitEvent> events)
    {
        var ordered = Order(events);
        if (ordered.Count == 0)
            return new ClaimState(claimId, ClaimStatus.NoResponse, billedCharge, 0m, 0m, null, null, [], false, 0m, 0, 0);

        var active = new List<RemitEvent>();
        var recouped = false;
        var unmatchedReversals = 0;
        foreach (var sameDay in ordered.GroupBy(e => e.PaymentDate))
        {
            // Reversals sort first within a payment date, so one remitted alongside the payment it reverses
            // gets a second try once that date's adjudications are active.
            var retry = new List<RemitEvent>();
            foreach (var remitEvent in sameDay)
            {
                if (remitEvent.Payment.StatusCode != ReversalStatus) active.Add(remitEvent);
                else if (TryCancel(active, remitEvent)) recouped = true;
                else retry.Add(remitEvent);
            }
            foreach (var reversal in retry)
            {
                if (TryCancel(active, reversal)) recouped = true;
                else unmatchedReversals++;
            }
        }

        var netPaid = ordered.Sum(e => e.Payment.Paid);
        var lastRemit = ordered[^1].PaymentDate;
        var current = active.Count > 0 ? active[^1] : null;
        if (current is null)
            return new ClaimState(claimId, ClaimStatus.Reversed, billedCharge, netPaid, 0m, null, lastRemit, [], recouped, 0m,
                ordered.Count, unmatchedReversals);

        var denialLines = DenialLinesOf(current.Payment);
        var status = current.Payment.StatusCode switch
        {
            DeniedStatus => ClaimStatus.Denied,
            var code when PaidStatuses.Contains(code) => denialLines.Count > 0 ? ClaimStatus.PartiallyDenied : ClaimStatus.Paid,
            _ => ClaimStatus.Unknown,
        };
        var isDenial = status is ClaimStatus.Denied or ClaimStatus.PartiallyDenied;
        var overpayment = Math.Max(0m, netPaid - current.Payment.Paid);

        return new ClaimState(claimId, status, billedCharge, netPaid,
            isDenial ? denialLines.Sum(d => d.Amount) : 0m,
            isDenial ? current.PaymentDate : null,
            lastRemit, isDenial ? denialLines : [], recouped, overpayment, ordered.Count, unmatchedReversals);
    }

    // A reversal cancels the most recent still-active adjudication with the same payer claim control number.
    private static bool TryCancel(List<RemitEvent> active, RemitEvent reversal)
    {
        var index = active.FindLastIndex(a => a.Payment.PayerClaimControlNumber == reversal.Payment.PayerClaimControlNumber);
        if (index < 0) return false;
        active.RemoveAt(index);
        return true;
    }

    public static IReadOnlyList<DenialLine> DenialLinesOf(ClaimPayment payment)
    {
        var lines = new List<DenialLine>();
        foreach (var adjustment in payment.Adjustments.Where(IsDenial))
            lines.Add(new DenialLine("CLAIM", adjustment.Group, adjustment.Reason, adjustment.Amount, payment.Remarks));
        foreach (var service in payment.Services)
            foreach (var adjustment in service.Adjustments.Where(IsDenial))
                lines.Add(new DenialLine(service.ProcedureCode, adjustment.Group, adjustment.Reason, adjustment.Amount, service.Remarks));
        return lines;
    }

    private static bool IsDenial(Adjustment adjustment) => adjustment.Group != "PR" && !ContractualReasons.Contains(adjustment.Reason);
}
