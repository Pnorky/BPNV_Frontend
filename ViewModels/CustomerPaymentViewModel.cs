using AvaloniaApp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AvaloniaApp.ViewModels;

public partial class CustomerPaymentViewModel : ObservableObject
{
    private readonly StoreApiClient _api;
    private readonly INotificationService _notifications;
    private Guid _idempotencyKey = Guid.NewGuid();
    [ObservableProperty] private IReadOnlyList<CustomerResponse> _customers = [];
    [ObservableProperty] private CustomerResponse? _selectedCustomer;
    [ObservableProperty] private decimal? _amount;
    [ObservableProperty] private ApiCustomerPaymentMethod _selectedPaymentMethod;
    [ObservableProperty] private string _receiptNumber = "";
    [ObservableProperty] private string _referenceNumber = "";
    [ObservableProperty] private string _checkBank = "";
    [ObservableProperty] private string _checkNumber = "";
    [ObservableProperty] private string _note = "";
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _statusMessage = "Select a customer and enter the payment details.";

    public IReadOnlyList<ApiCustomerPaymentMethod> PaymentMethods { get; }
    public bool IsCashierCollection { get; }
    public string OutstandingDisplay => SelectedCustomer is null ? "₱0.00" : SelectedCustomer.OutstandingDisplay;
    public string PaymentAmountDisplay => $"₱{Amount.GetValueOrDefault():N2}";
    public string RemainingBalanceDisplay => $"₱{Math.Max(0, SelectedCustomer?.OutstandingBalance - Amount.GetValueOrDefault() ?? 0):N2}";
    public string CashAvailabilityMessage => IsCashierCollection
        ? "Cash requires an issued receipt number and an open cashier shift."
        : "Admin cash collections require an issued receipt number and are not included in cashier shifts.";
    public bool IsCash => SelectedPaymentMethod == ApiCustomerPaymentMethod.Cash;
    public bool RequiresReference => SelectedPaymentMethod is ApiCustomerPaymentMethod.GCash or
        ApiCustomerPaymentMethod.BankTransfer or ApiCustomerPaymentMethod.Card;
    public bool IsCheck => SelectedPaymentMethod == ApiCustomerPaymentMethod.Check;
    public bool CanSubmit => !IsBusy && SelectedCustomer is { OutstandingBalance: > 0 } && Amount is > 0 &&
        Amount <= SelectedCustomer.OutstandingBalance &&
        (!IsCash || !string.IsNullOrWhiteSpace(ReceiptNumber)) &&
        (!RequiresReference || !string.IsNullOrWhiteSpace(ReferenceNumber)) &&
        (!IsCheck || !string.IsNullOrWhiteSpace(CheckBank) && !string.IsNullOrWhiteSpace(CheckNumber));

    public CustomerPaymentViewModel(StoreApiClient api, INotificationService notifications, bool isCashier)
    {
        _api = api;
        _notifications = notifications;
        IsCashierCollection = isCashier;
        PaymentMethods = Enum.GetValues<ApiCustomerPaymentMethod>();
        _selectedPaymentMethod = PaymentMethods[0];
        _ = LoadAsync();
    }

    partial void OnSelectedCustomerChanged(CustomerResponse? value) => NotifyState();
    partial void OnAmountChanged(decimal? value) => NotifyState();
    partial void OnSelectedPaymentMethodChanged(ApiCustomerPaymentMethod value)
    {
        ReceiptNumber = "";
        ReferenceNumber = "";
        CheckBank = "";
        CheckNumber = "";
        OnPropertyChanged(nameof(IsCash));
        OnPropertyChanged(nameof(RequiresReference));
        OnPropertyChanged(nameof(IsCheck));
        NotifyState();
    }
    partial void OnReceiptNumberChanged(string value) => NotifyState();
    partial void OnReferenceNumberChanged(string value) => NotifyState();
    partial void OnCheckBankChanged(string value) => NotifyState();
    partial void OnCheckNumberChanged(string value) => NotifyState();
    partial void OnIsBusyChanged(bool value) => OnPropertyChanged(nameof(CanSubmit));

    [RelayCommand]
    public async Task LoadAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            var response = await _api.GetCustomersAsync(includeInactive: true, pageSize: 100);
            Customers = response.Items.Where(customer => customer.OutstandingBalance > 0).ToArray();
        }
        catch (Exception exception) when (IsApiFailure(exception))
        {
            StatusMessage = UserFacingErrors.Get(exception, "Customer accounts could not be loaded.");
            _notifications.ShowError("Customers unavailable", StatusMessage);
        }
        finally { IsBusy = false; }
    }

    [RelayCommand(CanExecute = nameof(CanSubmit))]
    private async Task SubmitAsync()
    {
        if (!CanSubmit || SelectedCustomer is null || !Amount.HasValue) return;
        IsBusy = true;
        try
        {
            var payment = await _api.CreateCustomerPaymentAsync(SelectedCustomer.Id, new(_idempotencyKey, Amount.Value,
                SelectedPaymentMethod, Null(ReceiptNumber), Null(ReferenceNumber), Null(CheckBank), Null(CheckNumber), Null(Note)));
            _idempotencyKey = Guid.NewGuid();
            StatusMessage = $"Recorded {payment.PaymentNumber} for {payment.AmountDisplay}.";
            _notifications.ShowSuccess("Customer payment recorded", StatusMessage);
            Amount = null; ReceiptNumber = ReferenceNumber = CheckBank = CheckNumber = Note = "";
            IsBusy = false;
            await LoadAsync();
            SelectedCustomer = Customers.FirstOrDefault(customer => customer.Id == payment.CustomerId);
        }
        catch (Exception exception) when (IsApiFailure(exception))
        {
            StatusMessage = UserFacingErrors.Get(exception, "Customer payment could not be recorded.");
            _notifications.ShowError("Payment not recorded", StatusMessage);
        }
        finally { IsBusy = false; }
    }

    private void NotifyState()
    {
        OnPropertyChanged(nameof(OutstandingDisplay));
        OnPropertyChanged(nameof(PaymentAmountDisplay));
        OnPropertyChanged(nameof(RemainingBalanceDisplay));
        OnPropertyChanged(nameof(CanSubmit));
        SubmitCommand.NotifyCanExecuteChanged();
    }
    private static string? Null(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static bool IsApiFailure(Exception exception) => exception is ApiClientException or HttpRequestException or TaskCanceledException;
}
