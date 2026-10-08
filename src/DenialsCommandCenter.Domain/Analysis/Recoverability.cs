using DenialsCommandCenter.Domain.Claims;
using DenialsCommandCenter.Domain.Reference;

namespace DenialsCommandCenter.Domain.Analysis;

public enum RecoveryBucket { Recoverable, MaybeEligibility, LostWindowExpired, LostPolicy }

public sealed record Recoverability(RecoveryBucket Bucket, DateOnly? Deadline, int? DaysToDeadline, decimal ExpectedValue, decimal PriorityScore);

public static class RecoverabilityAssessor
{
    public static Recoverability Assess(
        ClaimState state, DateOnly dateOfService, DenialClassification classification, PayerRule? payerRule, decimal allowedRatio, DateOnly today)
    {
        DateOnly? deadline = null;
        if (payerRule is not null && state.DenialDate is { } denialDate)
        {
            var windowDays = classification.Action == NextActionType.CorrectedClaim
                ? payerRule.CorrectedClaimWindowDays
                : payerRule.AppealWindowDays;
            deadline = denialDate.AddDays(windowDays);
        }
        if (classification.Action == NextActionType.RequestRetroAuthorization)
        {
            var retroAuthorizationDeadline = dateOfService.AddDays(DenialClassifier.RetroAuthorizationDays);
            if (deadline is null || retroAuthorizationDeadline < deadline) deadline = retroAuthorizationDeadline;
        }
        int? daysToDeadline = deadline?.DayNumber - today.DayNumber;

        var bucket = daysToDeadline < 0
            ? RecoveryBucket.LostWindowExpired
            : classification.Action switch
            {
                NextActionType.WriteOff or NextActionType.CloseAsDuplicate => RecoveryBucket.LostPolicy,
                NextActionType.BillOtherCoverage => RecoveryBucket.MaybeEligibility,
                _ => RecoveryBucket.Recoverable,
            };

        // Money in a lost bucket is not coming back, so it must not count toward expected recovery.
        var expectedValue = bucket is RecoveryBucket.LostWindowExpired or RecoveryBucket.LostPolicy
            ? 0m
            : Math.Round(state.DeniedAmount * allowedRatio, 2, MidpointRounding.ToEven);
        var priorityScore = bucket == RecoveryBucket.Recoverable ? expectedValue * Urgency(daysToDeadline) : 0m;
        return new Recoverability(bucket, deadline, daysToDeadline, expectedValue, priorityScore);
    }

    public static int Urgency(int? daysToDeadline) => daysToDeadline switch
    {
        <= 14 => 3,
        <= 30 => 2,
        _ => 1,
    };
}

public static class AllowedRatios
{
    public static IReadOnlyDictionary<string, decimal> ByPayer(IEnumerable<RemitEvent> events) =>
        PaidAdjudications(events).GroupBy(e => e.PayerId).ToDictionary(g => g.Key, g => Ratio(g.ToList()));

    public static decimal Overall(IEnumerable<RemitEvent> events) => Ratio(PaidAdjudications(events).ToList());

    private static IEnumerable<RemitEvent> PaidAdjudications(IEnumerable<RemitEvent> events) =>
        events.Where(e => e.ClaimId is not null && e.Payment.StatusCode == "1" && e.Payment.Charge > 0);

    private static decimal Ratio(IReadOnlyCollection<RemitEvent> events)
    {
        var charge = events.Sum(e => e.Payment.Charge);
        return charge == 0 ? 0m : events.Sum(e => e.Payment.Paid + e.Payment.PatientResponsibility) / charge;
    }
}
