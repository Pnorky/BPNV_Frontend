using AvaloniaApp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AvaloniaApp.ViewModels;

public partial class CustomerAccountsViewModel : ObservableObject
{
    private readonly StoreApiClient _api;
    private readonly INotificationService _notifications;
    private int _vehicleLoadVersion;

    [ObservableProperty] private IReadOnlyList<CustomerResponse> _customers = [];
    [ObservableProperty] private CustomerResponse? _selectedCustomer;
    [ObservableProperty] private IReadOnlyList<CustomerVehicleResponse> _vehicles = [];
    [ObservableProperty] private CustomerVehicleResponse? _selectedVehicle;
    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private string _accountNumber = "";
    [ObservableProperty] private string _customerName = "";
    [ObservableProperty] private string _billingAddress = "";
    [ObservableProperty] private string _contactPerson = "";
    [ObservableProperty] private string _phone = "";
    [ObservableProperty] private string _email = "";
    [ObservableProperty] private string _plateOrUnitNumber = "";
    [ObservableProperty] private string _vehicleDescription = "";
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _isVehiclesLoading;
    [ObservableProperty] private string _statusMessage = "Loading customer accounts...";
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private string? _vehicleError;

    public bool IsFiltered => !string.IsNullOrWhiteSpace(SearchText);
    public bool HasSelectedCustomer => SelectedCustomer is not null;
    public bool HasSelectedVehicle => SelectedVehicle is not null;
    public bool CanManageCustomers { get; }
    public bool CanSaveCustomer => CanManageCustomers && !IsBusy &&
        !string.IsNullOrWhiteSpace(AccountNumber) && !string.IsNullOrWhiteSpace(CustomerName);
    public bool CanSaveVehicle => CanManageCustomers && !IsBusy && SelectedCustomer is not null &&
        !string.IsNullOrWhiteSpace(PlateOrUnitNumber);
    public bool CanDeactivateCustomer => CanManageCustomers && !IsBusy && SelectedCustomer?.IsActive == true;
    public bool CanReactivateCustomer => CanManageCustomers && !IsBusy && SelectedCustomer is { IsActive: false };
    public string CustomerFormTitle => SelectedCustomer is null ? "Create customer" : $"Customer details · {SelectedCustomer.Name}";
    public string CustomerSaveText => SelectedCustomer is null ? "Create customer" : "Save changes";
    public string VehicleSectionTitle => SelectedCustomer is null
        ? "Vehicles and equipment"
        : $"Vehicles and equipment · {SelectedCustomer.Name}";
    public string VehicleSaveText => SelectedVehicle is null ? "Add vehicle" : "Save vehicle";
    public string SelectedCustomerStatus => SelectedCustomer?.Status ?? "Select a customer row to view its details and vehicles.";

    public CustomerAccountsViewModel(StoreApiClient api, INotificationService notifications, bool canManageCustomers = true)
    {
        _api = api;
        _notifications = notifications;
        CanManageCustomers = canManageCustomers;
        _ = LoadAsync();
    }

    partial void OnSearchTextChanged(string value)
    {
        OnPropertyChanged(nameof(IsFiltered));
        _ = LoadAsync();
    }

    partial void OnSelectedCustomerChanged(CustomerResponse? value)
    {
        SelectedVehicle = null;
        Vehicles = [];
        VehicleError = null;
        var version = ++_vehicleLoadVersion;
        if (value is null)
        {
            IsVehiclesLoading = false;
            ClearCustomerFields();
        }
        else
        {
            AccountNumber = value.AccountNumber;
            CustomerName = value.Name;
            BillingAddress = value.BillingAddress ?? "";
            ContactPerson = value.ContactPerson ?? "";
            Phone = value.Phone ?? "";
            Email = value.Email ?? "";
            _ = LoadVehiclesAsync(value.Id, version);
        }
        NotifyCustomerEditor();
        NotifyVehicleEditor();
    }

    partial void OnSelectedVehicleChanged(CustomerVehicleResponse? value)
    {
        PlateOrUnitNumber = value?.PlateOrUnitNumber ?? "";
        VehicleDescription = value?.Description ?? "";
        NotifyVehicleEditor();
    }

    partial void OnAccountNumberChanged(string value) => NotifyCustomerEditor();
    partial void OnCustomerNameChanged(string value) => NotifyCustomerEditor();
    partial void OnPlateOrUnitNumberChanged(string value) => NotifyVehicleEditor();
    partial void OnIsBusyChanged(bool value) { NotifyCustomerEditor(); NotifyVehicleEditor(); }

    [RelayCommand]
    public async Task LoadAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var selectedId = SelectedCustomer?.Id;
            var response = await _api.GetCustomersAsync(SearchText, includeInactive: true, pageSize: 100);
            Customers = response.Items;
            SelectedCustomer = selectedId.HasValue ? Customers.FirstOrDefault(customer => customer.Id == selectedId) : null;
            StatusMessage = $"Loaded {response.TotalCount} customer account{(response.TotalCount == 1 ? "" : "s")}.";
        }
        catch (Exception exception) when (IsApiFailure(exception))
        {
            Customers = [];
            ErrorMessage = UserFacingErrors.Get(exception, "Customer accounts could not be loaded.");
            _notifications.ShowError("Customer accounts unavailable", ErrorMessage);
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private void NewCustomer()
    {
        SelectedCustomer = null;
        ClearCustomerFields();
        NotifyCustomerEditor();
    }

    [RelayCommand(CanExecute = nameof(CanSaveCustomer))]
    private async Task SaveCustomerAsync()
    {
        if (!CanSaveCustomer) return;
        IsBusy = true;
        try
        {
            var request = new SaveCustomerRequest(AccountNumber.Trim(), CustomerName.Trim(), Null(BillingAddress),
                Null(ContactPerson), Null(Phone), Null(Email), SelectedCustomer?.Version);
            var saved = SelectedCustomer is null
                ? await _api.CreateCustomerAsync(request)
                : await _api.UpdateCustomerAsync(SelectedCustomer.Id, request);
            StatusMessage = SelectedCustomer is null
                ? $"Created {saved.AccountNumber} · {saved.Name}."
                : $"Updated {saved.AccountNumber} · {saved.Name}.";
            _notifications.ShowSuccess("Customer saved", StatusMessage);
            IsBusy = false;
            await LoadAsync();
            SelectedCustomer = Customers.FirstOrDefault(customer => customer.Id == saved.Id);
        }
        catch (Exception exception) when (IsApiFailure(exception))
        {
            StatusMessage = UserFacingErrors.Get(exception, "Customer could not be saved.");
            _notifications.ShowError("Customer not saved", StatusMessage);
        }
        finally { IsBusy = false; }
    }

    [RelayCommand(CanExecute = nameof(CanDeactivateCustomer))]
    private Task DeactivateCustomerAsync() => SetCustomerActiveAsync(false);

    [RelayCommand(CanExecute = nameof(CanReactivateCustomer))]
    private Task ReactivateCustomerAsync() => SetCustomerActiveAsync(true);

    [RelayCommand]
    private void NewVehicle()
    {
        SelectedVehicle = null;
        PlateOrUnitNumber = VehicleDescription = "";
        NotifyVehicleEditor();
    }

    [RelayCommand(CanExecute = nameof(CanSaveVehicle))]
    private async Task SaveVehicleAsync()
    {
        if (!CanSaveVehicle || SelectedCustomer is null) return;
        IsBusy = true;
        try
        {
            var request = new SaveCustomerVehicleRequest(PlateOrUnitNumber.Trim(), Null(VehicleDescription), SelectedVehicle?.Version);
            if (SelectedVehicle is null)
                await _api.CreateCustomerVehicleAsync(SelectedCustomer.Id, request);
            else
                await _api.UpdateCustomerVehicleAsync(SelectedCustomer.Id, SelectedVehicle.Id, request);
            StatusMessage = SelectedVehicle is null ? "Vehicle added to the customer account." : "Vehicle details updated.";
            _notifications.ShowSuccess("Vehicle saved", StatusMessage);
            IsBusy = false;
            SelectedVehicle = null;
            await LoadVehiclesAsync(SelectedCustomer.Id, ++_vehicleLoadVersion);
        }
        catch (Exception exception) when (IsApiFailure(exception))
        {
            StatusMessage = UserFacingErrors.Get(exception, "Vehicle could not be saved.");
            _notifications.ShowError("Vehicle not saved", StatusMessage);
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private Task DeactivateVehicleAsync(CustomerVehicleResponse? vehicle) => SetVehicleActiveAsync(vehicle, false);

    [RelayCommand]
    private Task ReactivateVehicleAsync(CustomerVehicleResponse? vehicle) => SetVehicleActiveAsync(vehicle, true);

    [RelayCommand]
    private void ClearFilters() => SearchText = "";

    private async Task SetCustomerActiveAsync(bool active)
    {
        if (SelectedCustomer is null || IsBusy || !CanManageCustomers) return;
        var id = SelectedCustomer.Id;
        IsBusy = true;
        try
        {
            if (active) await _api.ReactivateCustomerAsync(id); else await _api.DeactivateCustomerAsync(id);
            StatusMessage = $"Customer {(active ? "reactivated" : "deactivated")}.";
            _notifications.ShowSuccess("Customer updated", StatusMessage);
            IsBusy = false;
            await LoadAsync();
            SelectedCustomer = Customers.FirstOrDefault(customer => customer.Id == id);
        }
        catch (Exception exception) when (IsApiFailure(exception))
        {
            StatusMessage = UserFacingErrors.Get(exception, "Customer status could not be changed.");
            _notifications.ShowError("Customer not updated", StatusMessage);
        }
        finally { IsBusy = false; }
    }

    private async Task SetVehicleActiveAsync(CustomerVehicleResponse? vehicle, bool active)
    {
        if (vehicle is null || SelectedCustomer is null || IsBusy || !CanManageCustomers) return;
        IsBusy = true;
        try
        {
            if (active) await _api.ReactivateCustomerVehicleAsync(SelectedCustomer.Id, vehicle.Id);
            else await _api.DeactivateCustomerVehicleAsync(SelectedCustomer.Id, vehicle.Id);
            StatusMessage = $"Vehicle {(active ? "reactivated" : "deactivated")}.";
            _notifications.ShowSuccess("Vehicle updated", StatusMessage);
            IsBusy = false;
            SelectedVehicle = null;
            await LoadVehiclesAsync(SelectedCustomer.Id, ++_vehicleLoadVersion);
        }
        catch (Exception exception) when (IsApiFailure(exception))
        {
            StatusMessage = UserFacingErrors.Get(exception, "Vehicle status could not be changed.");
            _notifications.ShowError("Vehicle not updated", StatusMessage);
        }
        finally { IsBusy = false; }
    }

    private async Task LoadVehiclesAsync(Guid customerId, int loadVersion)
    {
        IsVehiclesLoading = true;
        VehicleError = null;
        try
        {
            var vehicles = await _api.GetCustomerVehiclesAsync(customerId);
            if (loadVersion != _vehicleLoadVersion || SelectedCustomer?.Id != customerId) return;
            Vehicles = vehicles.OrderByDescending(vehicle => vehicle.IsActive)
                .ThenBy(vehicle => vehicle.PlateOrUnitNumber).ToArray();
        }
        catch (Exception exception) when (IsApiFailure(exception))
        {
            if (loadVersion == _vehicleLoadVersion)
                VehicleError = UserFacingErrors.Get(exception, "Vehicles could not be loaded.");
        }
        finally
        {
            if (loadVersion == _vehicleLoadVersion) IsVehiclesLoading = false;
        }
    }

    private void ClearCustomerFields() =>
        AccountNumber = CustomerName = BillingAddress = ContactPerson = Phone = Email = "";

    private void NotifyCustomerEditor()
    {
        OnPropertyChanged(nameof(HasSelectedCustomer));
        OnPropertyChanged(nameof(CustomerFormTitle));
        OnPropertyChanged(nameof(CustomerSaveText));
        OnPropertyChanged(nameof(SelectedCustomerStatus));
        OnPropertyChanged(nameof(CanSaveCustomer));
        OnPropertyChanged(nameof(CanDeactivateCustomer));
        OnPropertyChanged(nameof(CanReactivateCustomer));
        OnPropertyChanged(nameof(VehicleSectionTitle));
        SaveCustomerCommand.NotifyCanExecuteChanged();
        DeactivateCustomerCommand.NotifyCanExecuteChanged();
        ReactivateCustomerCommand.NotifyCanExecuteChanged();
    }

    private void NotifyVehicleEditor()
    {
        OnPropertyChanged(nameof(HasSelectedVehicle));
        OnPropertyChanged(nameof(VehicleSaveText));
        OnPropertyChanged(nameof(CanSaveVehicle));
        SaveVehicleCommand.NotifyCanExecuteChanged();
    }

    private static string? Null(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static bool IsApiFailure(Exception exception) => exception is ApiClientException or HttpRequestException or TaskCanceledException;
}
