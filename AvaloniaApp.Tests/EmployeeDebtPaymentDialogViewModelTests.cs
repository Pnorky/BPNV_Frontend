using AvaloniaApp.Services;
using AvaloniaApp.ViewModels;

namespace AvaloniaApp.Tests;

[TestClass]
public sealed class EmployeeDebtPaymentDialogViewModelTests
{
    [TestMethod]
    public void PartialAndExactCashPaymentsCanBeConfirmed()
    {
        var viewModel = new EmployeeDebtPaymentDialogViewModel(Employee(100m)) { Amount = 50m };
        Assert.IsTrue(viewModel.CanConfirm);
        Assert.AreEqual(50m, viewModel.CreateResult()!.Amount);

        viewModel.Amount = 100m;
        Assert.IsTrue(viewModel.CanConfirm);
    }

    [TestMethod]
    public void ZeroAndOverpaymentCannotBeConfirmed()
    {
        var viewModel = new EmployeeDebtPaymentDialogViewModel(Employee(100m));
        Assert.IsFalse(viewModel.CanConfirm);
        viewModel.Amount = 100.01m;
        Assert.IsFalse(viewModel.CanConfirm);
        Assert.IsNull(viewModel.CreateResult());
    }

    [TestMethod]
    public void GCashRequiresAndTrimsReference()
    {
        var viewModel = new EmployeeDebtPaymentDialogViewModel(Employee(100m))
        {
            Amount = 25m,
            PaymentMethod = ApiEmployeeDebtPaymentMethod.GCash
        };
        Assert.IsFalse(viewModel.CanConfirm);
        viewModel.ReferenceNumber = " REF-123 ";
        Assert.IsTrue(viewModel.CanConfirm);
        Assert.AreEqual("REF-123", viewModel.CreateResult()!.ReferenceNumber);
    }

    [TestMethod]
    public void InactiveEmployeeCannotReceivePayment()
    {
        var viewModel = new EmployeeDebtPaymentDialogViewModel(Employee(100m, false)) { Amount = 25m };
        Assert.IsFalse(viewModel.CanConfirm);
    }

    private static EmployeeBalanceResponse Employee(decimal outstanding, bool active = true) => new(
        Guid.NewGuid(), "EMP-000001", "Employee One", active, 200m, 100m, 100m, 100m - outstanding,
        outstanding, null);
}
