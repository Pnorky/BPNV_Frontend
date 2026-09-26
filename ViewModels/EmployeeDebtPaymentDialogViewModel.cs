using AvaloniaApp.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AvaloniaApp.ViewModels;

public sealed record EmployeeDebtPaymentDialogResult(
    decimal Amount, ApiEmployeeDebtPaymentMethod PaymentMethod, string? ReferenceNumber, string? Note);

public partial class EmployeeDebtPaymentDialogViewModel(EmployeeBalanceResponse employee) : ObservableObject
{
    [ObservableProperty] private decimal _amount;
    [ObservableProperty] private ApiEmployeeDebtPaymentMethod _paymentMethod = ApiEmployeeDebtPaymentMethod.Cash;
    [ObservableProperty] private string _referenceNumber = "";
    [ObservableProperty] private string _note = "";

    public string EmployeeDisplay => employee.EmployeeDisplay;
    public string OutstandingDisplay => employee.OutstandingBalanceDisplay;
    public bool IsGCash => PaymentMethod == ApiEmployeeDebtPaymentMethod.GCash;
    public bool CanConfirm => employee.IsActive && Amount > 0 && Amount <= employee.OutstandingBalance &&
                              (!IsGCash || !string.IsNullOrWhiteSpace(ReferenceNumber));
    public string ValidationMessage => !employee.IsActive ? "Inactive employees cannot receive new payments."
        : Amount <= 0 ? "Enter a payment amount greater than zero."
        : Amount > employee.OutstandingBalance ? $"Amount cannot exceed {employee.OutstandingBalanceDisplay}."
        : IsGCash && string.IsNullOrWhiteSpace(ReferenceNumber) ? "Enter the GCash reference number."
        : "";
    public bool HasValidationError => !CanConfirm;

    partial void OnAmountChanged(decimal value) => NotifyValidation();
    partial void OnPaymentMethodChanged(ApiEmployeeDebtPaymentMethod value) { OnPropertyChanged(nameof(IsGCash)); NotifyValidation(); }
    partial void OnReferenceNumberChanged(string value) => NotifyValidation();

    public EmployeeDebtPaymentDialogResult? CreateResult() => CanConfirm
        ? new(Amount, PaymentMethod, NullIfWhiteSpace(ReferenceNumber), NullIfWhiteSpace(Note))
        : null;

    private void NotifyValidation()
    {
        OnPropertyChanged(nameof(CanConfirm));
        OnPropertyChanged(nameof(ValidationMessage));
        OnPropertyChanged(nameof(HasValidationError));
    }

    private static string? NullIfWhiteSpace(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
