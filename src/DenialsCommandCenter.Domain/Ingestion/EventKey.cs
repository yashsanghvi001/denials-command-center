using System.Globalization;
using DenialsCommandCenter.Domain.X12;

namespace DenialsCommandCenter.Domain.Ingestion;

public static class EventKey
{
    public static string For(string payerId, string traceNumber, ClaimPayment claim) =>
        Hashing.Sha256Hex(string.Join('|', payerId, traceNumber, claim.RawClaimRef, claim.PayerClaimControlNumber,
            claim.StatusCode, claim.FrequencyCode, Money(claim.Charge), Money(claim.Paid)));

    public static string ForTransaction(RemittanceTransaction transaction) =>
        Hashing.Sha256Hex(string.Join('|', transaction.PayerId, transaction.TraceNumber, Money(transaction.PaymentAmount)));

    private static string Money(decimal amount) => amount.ToString("0.00", CultureInfo.InvariantCulture);
}
