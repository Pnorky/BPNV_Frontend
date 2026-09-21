using AvaloniaApp.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AvaloniaApp.ViewModels;

public sealed record PaymentDialogResult(
    ApiPaymentMethod PaymentMethod,
    decimal? AmountTendered,
    decimal Change,
    string? Reference = null);

public partial class PaymentDialogViewModel : ObservableObject
{
    [ObservableProperty] private ApiPaymentMethod _selectedPaymentMethod = ApiPaymentMethod.Cash;
    [ObservableProperty] private decimal? _amountTendered;
    [ObservableProperty] private bool _isGcashConfirmed;
    [ObservableProperty] private string _reference = "";

    public PaymentDialogViewModel(decimal total, bool isEmployeeSale = false)
    {
        if (total < 0) throw new ArgumentOutOfRangeException(nameof(total));
        Total = total;
        IsEmployeeSale = isEmployeeSale;
    }

    public decimal Total { get; }
    public bool IsEmployeeSale { get; }
    public bool IsEmployeeOwed => SelectedPaymentMethod == ApiPaymentMethod.EmployeeOwed;
    public bool IsCash => SelectedPaymentMethod == ApiPaymentMethod.Cash;
    public bool IsGCash => SelectedPaymentMethod == ApiPaymentMethod.GCash;
    public decimal Change => IsCash ? Math.Max(0m, (AmountTendered ?? 0m) - Total) : 0m;
    public bool CanConfirm => IsEmployeeOwed
        ? IsEmployeeSale
        : IsCash
        ? AmountTendered is >= 0m && AmountTendered >= Total
        : IsGcashConfirmed;
    public string TotalDisplay => $"₱{Total:N2}";
    public string ChangeDisplay => $"₱{Change:N2}";
    public string ValidationMessage => IsEmployeeOwed
        ? ""
        : IsCash
        ? AmountTendered switch
        {
            null => "Enter the amount tendered.",
            < 0m => "Amount tendered cannot be negative.",
            var amount when amount < Total => $"Amount tendered is ₱{Total - amount:N2} short.",
            _ => ""
        }
        : IsGcashConfirmed ? "" : "Confirm that the GCash payment was received.";
    public bool HasValidationError => ValidationMessage.Length > 0;

    public PaymentDialogResult? CreateResult() => CanConfirm
        ? new PaymentDialogResult(
            SelectedPaymentMethod,
            IsCash ? AmountTendered : null,
            Change,
            IsGCash ? Reference.Trim() : null)
        : null;

    partial void OnSelectedPaymentMethodChanged(ApiPaymentMethod value) => NotifyPaymentState();
    partial void OnAmountTenderedChanged(decimal? value) => NotifyPaymentState();
    partial void OnIsGcashConfirmedChanged(bool value) => NotifyPaymentState();

    private void NotifyPaymentState()
    {
        OnPropertyChanged(nameof(IsCash));
        OnPropertyChanged(nameof(IsGCash));
        OnPropertyChanged(nameof(Change));
        OnPropertyChanged(nameof(ChangeDisplay));
        OnPropertyChanged(nameof(CanConfirm));
        OnPropertyChanged(nameof(ValidationMessage));
        OnPropertyChanged(nameof(HasValidationError));
    }
}
