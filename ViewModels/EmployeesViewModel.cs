using AvaloniaApp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AvaloniaApp.ViewModels;

public partial class EmployeesViewModel : ObservableObject
{
    private readonly StoreApiClient _api;
    private readonly INotificationService _notifications;
    private IReadOnlyList<EmployeeResponse> _employees = [];
    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private string _employeeName = "";
    [ObservableProperty] private IReadOnlyList<EmployeeResponse> _filteredEmployees = [];
    [ObservableProperty] private string _statusMessage = "Loading employees...";
    [ObservableProperty] private bool _isBusy;

    public bool CanRestore => _api.IsSuperAdmin;

    public EmployeesViewModel(StoreApiClient api, INotificationService notifications)
    {
        _api = api;
        _notifications = notifications;
        _ = LoadAsync();
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    [RelayCommand]
    public async Task LoadAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            _employees = await _api.GetEmployeesAsync(includeInactive: true);
            ApplyFilter();
            StatusMessage = $"Loaded {_employees.Count} employee{(_employees.Count == 1 ? "" : "s")}.";
        }
        catch (Exception exception) when (IsApiFailure(exception))
        {
            _employees = [];
            ApplyFilter();
            ShowError("Employees could not be loaded", FailureMessage(exception));
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    public async Task CreateEmployeeAsync()
    {
        if (IsBusy) return;
        if (string.IsNullOrWhiteSpace(EmployeeName)) { ShowError("Employee not created", "Employee name is required."); return; }
        IsBusy = true;
        try
        {
            var employee = await _api.CreateEmployeeAsync(new CreateEmployeeRequest(EmployeeName.Trim()));
            _employees = _employees.Append(employee).ToArray();
            EmployeeName = "";
            ApplyFilter();
            StatusMessage = $"{employee.EmployeeNumber} · {employee.Name} created.";
            _notifications.ShowSuccess("Employee created", StatusMessage);
        }
        catch (Exception exception) when (IsApiFailure(exception)) { ShowError("Employee not created", FailureMessage(exception)); }
        finally { IsBusy = false; }
    }

    public async Task UpdateEmployeeAsync(EmployeeResponse employee, string name)
    {
        if (IsBusy || string.IsNullOrWhiteSpace(name)) return;
        IsBusy = true;
        try
        {
            await _api.UpdateEmployeeAsync(employee.Id, new UpdateEmployeeRequest(name.Trim()));
            IsBusy = false;
            await LoadAsync();
            StatusMessage = $"{employee.EmployeeNumber} updated.";
            _notifications.ShowSuccess("Employee updated", StatusMessage);
        }
        catch (Exception exception) when (IsApiFailure(exception)) { ShowError("Employee not updated", FailureMessage(exception)); }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    public async Task DeactivateEmployeeAsync(EmployeeResponse? employee)
    {
        if (employee is null || IsBusy) return;
        IsBusy = true;
        try
        {
            await _api.DeactivateEmployeeAsync(employee.Id);
            IsBusy = false;
            await LoadAsync();
            StatusMessage = $"{employee.EmployeeNumber} deactivated; purchase history was preserved.";
            _notifications.ShowSuccess("Employee deactivated", StatusMessage);
        }
        catch (Exception exception) when (IsApiFailure(exception)) { ShowError("Employee not deactivated", FailureMessage(exception)); }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    public async Task ReactivateEmployeeAsync(EmployeeResponse? employee)
    {
        if (employee is null || IsBusy) return;
        IsBusy = true;
        try
        {
            await _api.ReactivateEmployeeAsync(employee.Id);
            IsBusy = false;
            await LoadAsync();
            StatusMessage = $"{employee.EmployeeNumber} reactivated.";
            _notifications.ShowSuccess("Employee reactivated", StatusMessage);
        }
        catch (Exception exception) when (IsApiFailure(exception)) { ShowError("Employee not reactivated", FailureMessage(exception)); }
        finally { IsBusy = false; }
    }

    private void ApplyFilter()
    {
        var search = SearchText.Trim();
        FilteredEmployees = (search.Length == 0 ? _employees : _employees.Where(employee =>
            employee.EmployeeNumber.Contains(search, StringComparison.OrdinalIgnoreCase) ||
            employee.Name.Contains(search, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(employee => employee.Name).ThenBy(employee => employee.EmployeeNumber).ToArray();
    }

    private void ShowError(string title, string message) { StatusMessage = message; _notifications.ShowError(title, message); }
    private static bool IsApiFailure(Exception exception) => exception is ApiClientException or HttpRequestException or TaskCanceledException;
    private static string FailureMessage(Exception exception) => exception is HttpRequestException ? "Cannot reach the store API." : exception is TaskCanceledException ? "The store API did not respond in time." : exception.Message;
}
