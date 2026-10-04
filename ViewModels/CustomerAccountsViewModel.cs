using AvaloniaApp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AvaloniaApp.ViewModels;

public partial class CustomerAccountsViewModel : ObservableObject
{
    private readonly StoreApiClient _api;
    private readonly INotificationService _notifications;
    [ObservableProperty] private IReadOnlyList<CustomerResponse> _customers = [];
    [ObservableProperty] private CustomerResponse? _selectedCustomer;
    [ObservableProperty] private IReadOnlyList<CustomerVehicleResponse> _vehicles = [];
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
    [ObservableProperty] private string _statusMessage = "Loading customer accounts...";
    [ObservableProperty] private string? _errorMessage;

    public bool IsFiltered => !string.IsNullOrWhiteSpace(SearchText);
    public bool CanCreateVehicle => SelectedCustomer is not null && !string.IsNullOrWhiteSpace(PlateOrUnitNumber);
    public bool CanManageCustomers { get; }

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
        OnPropertyChanged(nameof(CanCreateVehicle));
        _ = LoadVehiclesAsync();
    }

    partial void OnPlateOrUnitNumberChanged(string value) => OnPropertyChanged(nameof(CanCreateVehicle));

    [RelayCommand]
    public async Task LoadAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var response = await _api.GetCustomersAsync(SearchText, pageSize: 100);
            Customers = response.Items;
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
    private async Task CreateCustomerAsync()
    {
        if (IsBusy || string.IsNullOrWhiteSpace(AccountNumber) || string.IsNullOrWhiteSpace(CustomerName))
        {
            _notifications.ShowError("Customer not created", "Enter an account number and customer name.");
            return;
        }
        IsBusy = true;
        try
        {
            var created = await _api.CreateCustomerAsync(new(AccountNumber.Trim(), CustomerName.Trim(), Null(BillingAddress),
                Null(ContactPerson), Null(Phone), Null(Email)));
            AccountNumber = CustomerName = BillingAddress = ContactPerson = Phone = Email = "";
            StatusMessage = $"Created {created.AccountNumber} · {created.Name}.";
            _notifications.ShowSuccess("Customer created", StatusMessage);
            IsBusy = false;
            await LoadAsync();
        }
        catch (Exception exception) when (IsApiFailure(exception))
        {
            StatusMessage = UserFacingErrors.Get(exception, "Customer could not be created.");
            _notifications.ShowError("Customer not created", StatusMessage);
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task CreateVehicleAsync()
    {
        if (SelectedCustomer is null || !CanCreateVehicle || IsBusy) return;
        IsBusy = true;
        try
        {
            await _api.CreateCustomerVehicleAsync(SelectedCustomer.Id, new(PlateOrUnitNumber.Trim(), Null(VehicleDescription)));
            PlateOrUnitNumber = VehicleDescription = "";
            StatusMessage = "Plate or unit added to the customer account.";
            _notifications.ShowSuccess("Plate or unit added", StatusMessage);
            IsBusy = false;
            await LoadVehiclesAsync();
        }
        catch (Exception exception) when (IsApiFailure(exception))
        {
            StatusMessage = UserFacingErrors.Get(exception, "Plate or unit could not be added.");
            _notifications.ShowError("Plate or unit not added", StatusMessage);
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private void ClearFilters() => SearchText = "";

    private async Task LoadVehiclesAsync()
    {
        if (SelectedCustomer is null) { Vehicles = []; return; }
        try { Vehicles = await _api.GetCustomerVehiclesAsync(SelectedCustomer.Id); }
        catch (Exception exception) when (IsApiFailure(exception)) { StatusMessage = UserFacingErrors.Get(exception, "Plates and units could not be loaded."); }
    }

    private static string? Null(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static bool IsApiFailure(Exception exception) => exception is ApiClientException or HttpRequestException or TaskCanceledException;
}
