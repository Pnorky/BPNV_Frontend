using AvaloniaApp.Services;
using AvaloniaApp.ViewModels;

namespace AvaloniaApp.Tests;

[TestClass]
public sealed class PaymentDialogViewModelTests
{
    [TestMethod]
    public void CashExactAmountCanConfirmWithZeroChange()
    {
        var viewModel = new PaymentDialogViewModel(125.50m) { AmountTendered = 125.50m };

        Assert.IsTrue(viewModel.CanConfirm);
        Assert.AreEqual(0m, viewModel.Change);
        Assert.AreEqual(ApiPaymentMethod.Cash, viewModel.CreateResult()!.PaymentMethod);
    }

    [TestMethod]
    public void CashPositiveChangeCanConfirm()
    {
        var viewModel = new PaymentDialogViewModel(125.50m) { AmountTendered = 150m };

        Assert.IsTrue(viewModel.CanConfirm);
        Assert.AreEqual(24.50m, viewModel.Change);
        Assert.AreEqual(150m, viewModel.CreateResult()!.AmountTendered);
    }

    [TestMethod]
    public void CashInsufficientAmountCannotConfirmAndChangeStaysNonNegative()
    {
        var viewModel = new PaymentDialogViewModel(125.50m) { AmountTendered = 100m };

        Assert.IsFalse(viewModel.CanConfirm);
        Assert.AreEqual(0m, viewModel.Change);
        Assert.IsTrue(viewModel.HasValidationError);
        Assert.IsNull(viewModel.CreateResult());
    }

    [TestMethod]
    public void GCashRequiresCashierConfirmation()
    {
        var viewModel = new PaymentDialogViewModel(125.50m)
        {
            SelectedPaymentMethod = ApiPaymentMethod.GCash
        };

        Assert.IsFalse(viewModel.CanConfirm);
        Assert.IsNull(viewModel.CreateResult());

        viewModel.IsGcashConfirmed = true;

        Assert.IsTrue(viewModel.CanConfirm);
        Assert.AreEqual(ApiPaymentMethod.GCash, viewModel.CreateResult()!.PaymentMethod);
        Assert.IsNull(viewModel.CreateResult()!.AmountTendered);
    }
}
