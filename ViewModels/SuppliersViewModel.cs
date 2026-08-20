using AvaloniaApp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AvaloniaApp.ViewModels;

public partial class SuppliersViewModel : ObservableObject
{
    private readonly StoreApiClient _api;
    private IReadOnlyList<SupplierResponse> _suppliers = [];

    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private IReadOnlyList<SupplierResponse> _filteredSuppliers = [];
    [ObservableProperty] private string _supplierName = "";
    [ObservableProperty] private string _contactPerson = "";
    [ObservableProperty] private string _phone = "";
    [ObservableProperty] private string _statusMessage = "Loading suppliers...";
    [ObservableProperty] private bool _isBusy;

    public SuppliersViewModel(StoreApiClient api)
    {
        _api = api;
        _ = LoadAsync();
    }

    public bool CanRestore => _api.IsSuperAdmin;

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    [RelayCommand]
    public async Task LoadAsync()
    {
        if (IsBusy) return;
        StatusMessage = "Loading suppliers...";
        IsBusy = true;
        try
        {
            _suppliers = await _api.GetSuppliersAsync(includeInactive: true);
            ApplyFilter();
            StatusMessage = $"Loaded {_suppliers.Count} supplier{(_suppliers.Count == 1 ? "" : "s")} from the database.";
        }
        catch (Exception exception) when (IsApiFailure(exception))
        {
            _suppliers = [];
            ApplyFilter();
            StatusMessage = FailureMessage(exception);
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task UpdateSupplierAsync(SupplierResponse supplier, string name, string? contactPerson, string? phone)
    {
        if (IsBusy) return;
        if (string.IsNullOrWhiteSpace(name)) { StatusMessage = "Supplier name is required."; return; }
        IsBusy = true;
        try
        {
            var updated = await _api.UpdateSupplierAsync(supplier.Id,
                new UpdateSupplierRequest(name.Trim(), NullIfWhiteSpace(contactPerson), NullIfWhiteSpace(phone)));
            IsBusy = false;
            await LoadAsync();
            StatusMessage = $"Supplier {updated.Name} updated.";
        }
        catch (Exception exception) when (IsApiFailure(exception)) { StatusMessage = FailureMessage(exception); }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    public async Task DeleteSupplierAsync(SupplierResponse? supplier)
    {
        if (supplier is null || IsBusy) return;
        IsBusy = true;
        try
        {
            await _api.DeactivateSupplierAsync(supplier.Id);
            IsBusy = false;
            await LoadAsync();
            StatusMessage = $"Supplier {supplier.Name} deactivated.";
        }
        catch (Exception exception) when (IsApiFailure(exception)) { StatusMessage = FailureMessage(exception); }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    public async Task ReactivateSupplierAsync(SupplierResponse? supplier)
    {
        if (supplier is null || IsBusy) return;
        IsBusy = true;
        try
        {
            await _api.ReactivateSupplierAsync(supplier.Id);
            IsBusy = false;
            await LoadAsync();
            StatusMessage = $"Supplier {supplier.Name} reactivated.";
        }
        catch (Exception exception) when (IsApiFailure(exception)) { StatusMessage = FailureMessage(exception); }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    public async Task CreateSupplierAsync()
    {
        if (IsBusy) return;
        if (string.IsNullOrWhiteSpace(SupplierName))
        {
            StatusMessage = "Supplier name is required.";
            return;
        }

        StatusMessage = "Creating supplier...";
        IsBusy = true;
        try
        {
            var supplier = await _api.CreateSupplierAsync(new CreateSupplierRequest(
                SupplierName.Trim(), NullIfWhiteSpace(ContactPerson), NullIfWhiteSpace(Phone)));
            _suppliers = _suppliers.Append(supplier).ToArray();
            SupplierName = "";
            ContactPerson = "";
            Phone = "";
            ApplyFilter();
            StatusMessage = $"Supplier {supplier.Name} created.";
        }
        catch (Exception exception) when (IsApiFailure(exception))
        {
            StatusMessage = FailureMessage(exception);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ApplyFilter()
    {
        var search = SearchText.Trim();
        FilteredSuppliers = (search.Length == 0
                ? _suppliers
                : _suppliers.Where(supplier =>
                    supplier.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    supplier.ContactPerson?.Contains(search, StringComparison.OrdinalIgnoreCase) == true ||
                    supplier.Phone?.Contains(search, StringComparison.OrdinalIgnoreCase) == true))
            .OrderBy(supplier => supplier.Name)
            .ToArray();
    }

    private static string? NullIfWhiteSpace(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static bool IsApiFailure(Exception exception) => exception is ApiClientException or HttpRequestException or TaskCanceledException;
    private static string FailureMessage(Exception exception) => exception switch
    {
        HttpRequestException => "Cannot reach the store API.",
        TaskCanceledException => "The store API did not respond in time.",
        _ => exception.Message
    };
}
