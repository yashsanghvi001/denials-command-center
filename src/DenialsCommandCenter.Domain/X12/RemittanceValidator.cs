using System.Globalization;

namespace DenialsCommandCenter.Domain.X12;

public static class RemittanceValidator
{
    public static string? ClaimProblem(ClaimPayment claim)
    {
        var adjustments = claim.Adjustments.Sum(a => a.Amount) + claim.Services.Sum(s => s.Adjustments.Sum(a => a.Amount));
        var imbalance = claim.Charge - claim.Paid - adjustments;
        if (imbalance != 0)
            return $"Charge {FormatMoney(claim.Charge)} minus paid {FormatMoney(claim.Paid)} minus adjustments {FormatMoney(adjustments)} leaves {FormatMoney(imbalance)}.";
        var servicePaid = claim.Services.Sum(s => s.Paid);
        if (claim.Services.Count > 0 && servicePaid != claim.Paid)
            return $"Service payments {FormatMoney(servicePaid)} do not equal claim payment {FormatMoney(claim.Paid)}.";
        return null;
    }

    public static decimal TransactionImbalance(RemittanceTransaction transaction) =>
        transaction.PaymentAmount - (transaction.Claims.Sum(c => c.Paid) - transaction.ProviderAdjustments.Sum(p => p.Amount));

    private static string FormatMoney(decimal amount) => amount.ToString("$#,##0.00;-$#,##0.00", CultureInfo.InvariantCulture);
}
