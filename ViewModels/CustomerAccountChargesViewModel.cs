using System.Collections.ObjectModel;
using AvaloniaApp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AvaloniaApp.ViewModels;

public partial class CustomerAccountChargeLineViewModel(Action changed) : ObservableObject
{
    [ObservableProperty] private string _particular = "";
    [ObservableProperty] private decimal _quantity = 1;
    [ObservableProperty] private string _unit = "";
    [ObservableProperty] private decimal _unitPrice;

    public decimal Amount
    {
        get
        {
            return TryGetAmount(out var amount) ? amount : 0;
        }
    }
    public string AmountDisplay => $"₱{Amount:N2}";
    public bool IsValid => !string.IsNullOrWhiteSpace(Particular) && Particular.Trim().Length <= 200 &&
        Quantity > 0 && Quantity <= 999999999999999.999m && decimal.Round(Quantity, 3) == Quantity &&
        !string.IsNullOrWhiteSpace(Unit) && Unit.Trim().Length <= 30 && UnitPrice >= 0 &&
        UnitPrice <= 99999999999999.9999m && decimal.Round(UnitPrice, 4) == UnitPrice && TryGetAmount(out _);

    partial void OnParticularChanged(string value) => Notify();
    partial void OnQuantityChanged(decimal value) => Notify();
    partial void OnUnitChanged(string value) => Notify();
    partial void OnUnitPriceChanged(decimal value) => Notify();

    private void Notify()
    {
        OnPropertyChanged(nameof(Amount));
        OnPropertyChanged(nameof(AmountDisplay));
        OnPropertyChanged(nameof(IsValid));
        changed();
    }
    private bool TryGetAmount(out decimal amount)
    {
        try
        {
            amount = decimal.Round(checked(Quantity * UnitPrice), 2, MidpointRounding.AwayFromZero);
            return amount <= 9999999999999999.99m;
        }
        catch (OverflowException)
        {
            amount = 0;
            return false;
        }
    }
}

public partial class CustomerAccountChargesViewModel : ObservableObject
{
    private readonly StoreApiClient _api;
    private readonly INotificationService _notifications;
    private Guid _idempotencyKey = Guid.NewGuid();
    private bool _submissionAttempted;
    private int _vehicleLoadVersion;
    [ObservableProperty] private IReadOnlyList<CustomerResponse> _customers = [];
    [ObservableProperty] private CustomerResponse? _selectedCustomer;
    [ObservableProperty] private IReadOnlyList<CustomerVehicleResponse> _vehicles = [];
    [ObservableProperty] private CustomerVehicleResponse? _selectedVehicle;
    [ObservableProperty] private DateTimeOffset? _chargeDate = StoreDateTime.AtStoreMidnight(StoreDateTime.StoreToday);
    [ObservableProperty] private string _invoiceReference = "";
    [ObservableProperty] private string _note = "";
    [ObservableProperty] private IReadOnlyList<CustomerAccountChargeResponse> _charges = [];
    [ObservableProperty] private CustomerAccountChargeResponse? _selectedCharge;
    [ObservableProperty] private string _voidReason = "";
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _isHistoryLoading;
    [ObservableProperty] private string? _historyError;
    [ObservableProperty] private int _historyPage = 1;
    [ObservableProperty] private int _historyPageSize = 20;
    [ObservableProperty] private int _historyTotalCount;
    [ObservableProperty] private string _statusMessage = "Record non-inventory customer charges without affecting stock.";

    public ObservableCollection<CustomerAccountChargeLineViewModel> Lines { get; } = [];
    public decimal ChargeTotal
    {
        get
        {
            try { return Lines.Aggregate(0m, (total, line) => checked(total + line.Amount)); }
            catch (OverflowException) { return decimal.MaxValue; }
        }
    }
    public string ChargeTotalDisplay => $"₱{ChargeTotal:N2}";
    public bool CanSave => !IsBusy && SelectedCustomer is { IsActive: true } && ChargeDate.HasValue &&
        !string.IsNullOrWhiteSpace(InvoiceReference) && InvoiceReference.Trim().Length <= 100 && Lines.Count > 0 &&
        Lines.All(line => line.IsValid) && ChargeTotal is > 0 and <= 9999999999999999.99m;
    public bool CanVoid => !IsBusy && SelectedCharge?.CanVoid == true && !string.IsNullOrWhiteSpace(VoidReason);
    public bool CanPreviousHistory => !IsHistoryLoading && HistoryPage > 1;
    public bool CanNextHistory => !IsHistoryLoading && HistoryPage * HistoryPageSize < HistoryTotalCount;

    public CustomerAccountChargesViewModel(StoreApiClient api, INotificationService notifications)
    {
        _api = api;
        _notifications = notifications;
        Lines.CollectionChanged += (_, _) => FormChanged();
        AddBlankLine();
        _ = LoadAsync();
    }

    partial void OnSelectedCustomerChanged(CustomerResponse? value)
    {
        SelectedVehicle = null;
        Vehicles = [];
        var loadVersion = ++_vehicleLoadVersion;
        FormChanged();
        if (value is not null) _ = LoadVehiclesAsync(value.Id, loadVersion);
    }
    partial void OnSelectedVehicleChanged(CustomerVehicleResponse? value) => FormChanged();
    partial void OnChargeDateChanged(DateTimeOffset? value) => FormChanged();
    partial void OnInvoiceReferenceChanged(string value) => FormChanged();
    partial void OnNoteChanged(string value) => FormChanged();
    partial void OnIsBusyChanged(bool value) { NotifyForm(); NotifyVoid(); }
    partial void OnHistoryPageSizeChanged(int value) { HistoryPage = 1; _ = LoadHistoryAsync(); }
    partial void OnIsHistoryLoadingChanged(bool value) => NotifyHistoryPaging();
    partial void OnSelectedChargeChanged(CustomerAccountChargeResponse? value) => NotifyVoid();
    partial void OnVoidReasonChanged(string value) => NotifyVoid();

    [RelayCommand]
    private void AddLine() => AddBlankLine();

    [RelayCommand]
    private void RemoveLine(CustomerAccountChargeLineViewModel line)
    {
        if (Lines.Count == 1)
        {
            line.Particular = "";
            line.Quantity = 1;
            line.Unit = "";
            line.UnitPrice = 0;
            return;
        }
        Lines.Remove(line);
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync()
    {
        if (!CanSave || SelectedCustomer is null || !ChargeDate.HasValue) return;
        IsBusy = true;
        try
        {
            var request = new CreateCustomerAccountChargeRequest(_idempotencyKey,
                DateOnly.FromDateTime(ChargeDate.Value.Date), InvoiceReference.Trim(), SelectedVehicle?.Id,
                Null(Note), Lines.Select(line => new CreateCustomerAccountChargeLineRequest(line.Particular.Trim(),
                    line.Quantity, line.Unit.Trim(), line.UnitPrice)).ToArray());
            _submissionAttempted = true;
            var charge = await _api.CreateCustomerAccountChargeAsync(SelectedCustomer.Id, request);
            _idempotencyKey = Guid.NewGuid();
            _submissionAttempted = false;
            StatusMessage = $"Recorded {charge.ChargeNumber} for {charge.TotalDisplay}.";
            _notifications.ShowSuccess("Account charge recorded", StatusMessage);
            ResetForm();
            await LoadCustomersAsync();
            await LoadHistoryAsync();
        }
        catch (Exception exception) when (IsApiFailure(exception))
        {
            StatusMessage = UserFacingErrors.Get(exception, "The account charge could not be recorded.");
            _notifications.ShowError("Charge not recorded", StatusMessage);
        }
        finally { IsBusy = false; }
    }

    [RelayCommand(CanExecute = nameof(CanVoid))]
    private async Task VoidSelectedAsync()
    {
        if (!CanVoid || SelectedCharge is null) return;
        IsBusy = true;
        try
        {
            var charge = await _api.VoidCustomerAccountChargeAsync(SelectedCharge.Id,
                new VoidCustomerAccountChargeRequest(VoidReason.Trim()));
            StatusMessage = $"Voided {charge.ChargeNumber}.";
            _notifications.ShowSuccess("Account charge voided", StatusMessage);
            VoidReason = "";
            SelectedCharge = null;
            await LoadCustomersAsync();
            await LoadHistoryAsync();
        }
        catch (Exception exception) when (IsApiFailure(exception))
        {
            StatusMessage = UserFacingErrors.Get(exception, "The account charge could not be voided.");
            _notifications.ShowError("Charge not voided", StatusMessage);
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private Task RetryHistoryAsync() => LoadHistoryAsync();

    [RelayCommand(CanExecute = nameof(CanPreviousHistory))]
    private async Task PreviousHistoryAsync()
    {
        if (!CanPreviousHistory) return;
        HistoryPage--;
        await LoadHistoryAsync();
    }

    [RelayCommand(CanExecute = nameof(CanNextHistory))]
    private async Task NextHistoryAsync()
    {
        if (!CanNextHistory) return;
        HistoryPage++;
        await LoadHistoryAsync();
    }

    private async Task LoadAsync()
    {
        await LoadCustomersAsync();
        await LoadHistoryAsync();
    }

    private async Task LoadCustomersAsync()
    {
        try
        {
            var selectedId = SelectedCustomer?.Id;
            var response = await _api.GetCustomersAsync(pageSize: 100);
            Customers = response.Items.Where(customer => customer.IsActive).OrderBy(customer => customer.Name).ToArray();
            SelectedCustomer = Customers.FirstOrDefault(customer => customer.Id == selectedId) ?? SelectedCustomer;
        }
        catch (Exception exception) when (IsApiFailure(exception))
        {
            StatusMessage = UserFacingErrors.Get(exception, "Customers could not be loaded.");
        }
    }

    private async Task LoadVehiclesAsync(Guid customerId, int loadVersion)
    {
        try
        {
            var vehicles = (await _api.GetCustomerVehiclesAsync(customerId)).Where(vehicle => vehicle.IsActive).ToArray();
            if (loadVersion == _vehicleLoadVersion && SelectedCustomer?.Id == customerId) Vehicles = vehicles;
        }
        catch (Exception exception) when (IsApiFailure(exception))
        {
            _notifications.ShowError("Vehicles unavailable", UserFacingErrors.Get(exception, "Customer vehicles could not be loaded."));
        }
    }

    private async Task LoadHistoryAsync()
    {
        if (IsHistoryLoading) return;
        IsHistoryLoading = true;
        HistoryError = null;
        try
        {
            var response = await _api.GetCustomerAccountChargesAsync(page: HistoryPage, pageSize: HistoryPageSize);
            Charges = response.Items;
            HistoryTotalCount = response.TotalCount;
        }
        catch (Exception exception) when (IsApiFailure(exception))
        {
            HistoryError = UserFacingErrors.Get(exception, "Account charge history could not be loaded.");
        }
        finally { IsHistoryLoading = false; NotifyHistoryPaging(); }
    }

    private void AddBlankLine() => Lines.Add(new CustomerAccountChargeLineViewModel(FormChanged));
    private void ResetForm()
    {
        InvoiceReference = Note = "";
        SelectedVehicle = null;
        Lines.Clear();
        AddBlankLine();
    }
    private void NotifyForm()
    {
        OnPropertyChanged(nameof(ChargeTotal));
        OnPropertyChanged(nameof(ChargeTotalDisplay));
        OnPropertyChanged(nameof(CanSave));
        SaveCommand.NotifyCanExecuteChanged();
    }
    private void FormChanged()
    {
        if (_submissionAttempted)
        {
            _idempotencyKey = Guid.NewGuid();
            _submissionAttempted = false;
        }
        NotifyForm();
    }
    private void NotifyVoid()
    {
        OnPropertyChanged(nameof(CanVoid));
        VoidSelectedCommand.NotifyCanExecuteChanged();
    }
    private void NotifyHistoryPaging()
    {
        OnPropertyChanged(nameof(CanPreviousHistory));
        OnPropertyChanged(nameof(CanNextHistory));
        PreviousHistoryCommand.NotifyCanExecuteChanged();
        NextHistoryCommand.NotifyCanExecuteChanged();
    }
    private static string? Null(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static bool IsApiFailure(Exception exception) => exception is ApiClientException or HttpRequestException or TaskCanceledException;
}
