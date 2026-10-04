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
    [ObservableProperty] private ApiCustomerPaymentMethod _selectedPaymentMethod = ApiCustomerPaymentMethod.Cash;
    [ObservableProperty] private string _referenceNumber = "";
    [ObservableProperty] private string _note = "";
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _statusMessage = "Select a customer and enter the payment details.";

    public IReadOnlyList<ApiCustomerPaymentMethod> PaymentMethods { get; } = Enum.GetValues<ApiCustomerPaymentMethod>();
    public string OutstandingDisplay => SelectedCustomer is null ? "₱0.00" : SelectedCustomer.OutstandingDisplay;
    public bool CanSubmit => !IsBusy && SelectedCustomer is { OutstandingBalance: > 0 } && Amount is > 0 &&
        Amount <= SelectedCustomer.OutstandingBalance && (SelectedPaymentMethod != ApiCustomerPaymentMethod.GCash || !string.IsNullOrWhiteSpace(ReferenceNumber));

    public CustomerPaymentViewModel(StoreApiClient api, INotificationService notifications)
    {
        _api = api;
        _notifications = notifications;
        _ = LoadAsync();
    }

    partial void OnSelectedCustomerChanged(CustomerResponse? value) => NotifyState();
    partial void OnAmountChanged(decimal? value) => NotifyState();
    partial void OnSelectedPaymentMethodChanged(ApiCustomerPaymentMethod value) => NotifyState();
    partial void OnReferenceNumberChanged(string value) => NotifyState();
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
                SelectedPaymentMethod, Null(ReferenceNumber), Null(Note)));
            _idempotencyKey = Guid.NewGuid();
            StatusMessage = $"Recorded {payment.PaymentNumber} for {payment.AmountDisplay}.";
            _notifications.ShowSuccess("Customer payment recorded", StatusMessage);
            Amount = null; ReferenceNumber = Note = "";
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
        OnPropertyChanged(nameof(CanSubmit));
        SubmitCommand.NotifyCanExecuteChanged();
    }
    private static string? Null(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static bool IsApiFailure(Exception exception) => exception is ApiClientException or HttpRequestException or TaskCanceledException;
}
