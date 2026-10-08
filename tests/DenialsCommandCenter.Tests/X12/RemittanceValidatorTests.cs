using DenialsCommandCenter.Domain.X12;

namespace DenialsCommandCenter.Tests.X12;

public class RemittanceValidatorTests
{
    private static readonly RemittanceTransaction Transaction = X12Parser.Parse(X12Samples.Basic).Transactions[0];

    [Fact]
    public void Balanced_claim_has_no_problem() => Assert.Null(RemittanceValidator.ClaimProblem(Transaction.Claims[0]));

    [Fact]
    public void Claim_whose_adjustments_do_not_add_up_is_reported()
    {
        var bad = Transaction.Claims[0] with { Charge = 410m };
        Assert.Contains("leaves $10.00", RemittanceValidator.ClaimProblem(bad));
    }

    [Fact]
    public void Service_payments_must_sum_to_claim_payment()
    {
        var claim = Transaction.Claims[0];
        var bad = claim with { Paid = 240m, Adjustments = [new Adjustment("CO", "45", 10m)] };
        Assert.Contains("Service payments $250.00", RemittanceValidator.ClaimProblem(bad));
    }

    [Fact]
    public void Balanced_transaction_has_zero_imbalance() => Assert.Equal(0m, RemittanceValidator.TransactionImbalance(Transaction));

    [Fact]
    public void Provider_level_adjustments_are_subtracted_from_claim_payments()
    {
        var withPlb = Transaction with { PaymentAmount = 88.80m, ProviderAdjustments = [new ProviderAdjustment("WO", "X", 161.20m)] };
        Assert.Equal(0m, RemittanceValidator.TransactionImbalance(withPlb));
    }
}
