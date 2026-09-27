using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using AvaloniaApp.Services;
using AvaloniaApp.Views.Dialogs;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AvaloniaApp.ViewModels;

public partial class EmployeeBalancesViewModel : ObservableObject
{
    private readonly StoreApiClient _api;
    private readonly INotificationService _notifications;
    private IReadOnlyList<EmployeeBalanceResponse> _allBalances = [];
    private Guid _paymentKey = Guid.NewGuid();
    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private IReadOnlyList<EmployeeBalanceResponse> _balances = [];
    [ObservableProperty] private EmployeeBalanceSummaryResponse _summary = new(0, 0, 0, 0, 0);
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _statusMessage = "Loading employee balances...";
    [ObservableProperty] private string? _errorMessage;

    public string OutstandingDisplay => $"₱{Balances.Sum(item => item.OutstandingBalance):N2}";
    public string EmployeesWithBalanceDisplay => Balances.Count(item => item.OutstandingBalance > 0).ToString("N0");
    public string RepaymentsDisplay => $"₱{Balances.Sum(item => item.Repayments):N2}";
    public string FullyPaidDisplay => Balances.Count(item => item.OutstandingBalance <= 0).ToString("N0");
    public string LargestBalanceDisplay => Balances.Where(item => item.OutstandingBalance > 0)
        .OrderByDescending(item => item.OutstandingBalance)
        .Select(item => $"{item.EmployeeName} · ₱{item.OutstandingBalance:N2}")
        .FirstOrDefault() ?? "None";
    public bool IsFiltered => !string.IsNullOrWhiteSpace(SearchText);

    public EmployeeBalancesViewModel(StoreApiClient api, INotificationService notifications)
    {
        _api = api;
        _notifications = notifications;
        _ = LoadAsync();
    }

    partial void OnSearchTextChanged(string value)
    {
        ApplyFilter();
        OnPropertyChanged(nameof(IsFiltered));
    }

    partial void OnSummaryChanged(EmployeeBalanceSummaryResponse value)
    {
        OnPropertyChanged(nameof(OutstandingDisplay));
        OnPropertyChanged(nameof(EmployeesWithBalanceDisplay));
        OnPropertyChanged(nameof(RepaymentsDisplay));
        OnPropertyChanged(nameof(FullyPaidDisplay));
        OnPropertyChanged(nameof(LargestBalanceDisplay));
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var response = await _api.GetEmployeeBalancesAsync(pageSize: 100);
            _allBalances = response.Items;
            Summary = response.Summary;
            ApplyFilter();
            StatusMessage = $"Loaded {response.TotalCount} employee balance{(response.TotalCount == 1 ? "" : "s")}.";
        }
        catch (Exception exception) when (IsApiFailure(exception))
        {
            _allBalances = [];
            ApplyFilter();
            ErrorMessage = FailureMessage(exception);
            ShowError("Employee balances could not be loaded", ErrorMessage);
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private void ClearFilters() => SearchText = "";

    [RelayCommand]
    private async Task RecordPaymentAsync(EmployeeBalanceResponse? employee)
    {
        if (employee is null || !employee.CanRecordPayment || IsBusy || MainWindow() is not { } owner) return;
        var dialog = new EmployeeDebtPaymentDialog { DataContext = new EmployeeDebtPaymentDialogViewModel(employee) };
        var result = await dialog.ShowDialog<EmployeeDebtPaymentDialogResult?>(owner);
        if (result is null) return;
        IsBusy = true;
        try
        {
            var payment = await _api.CreateEmployeeDebtPaymentAsync(employee.EmployeeId, new(
                _paymentKey, result.Amount, result.PaymentMethod, result.ReferenceNumber, result.Note));
            _paymentKey = Guid.NewGuid();
            StatusMessage = $"Recorded {payment.AmountDisplay} from {employee.EmployeeDisplay}.";
            _notifications.ShowSuccess("Employee payment recorded", StatusMessage);
            IsBusy = false;
            await LoadAsync();
        }
        catch (Exception exception) when (IsApiFailure(exception))
        {
            ShowError("Payment could not be recorded", FailureMessage(exception));
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task ShowPaymentHistoryAsync(EmployeeBalanceResponse? employee)
    {
        if (employee is null || IsBusy || MainWindow() is not { } owner) return;
        IsBusy = true;
        try
        {
            var payments = await _api.GetEmployeeDebtPaymentsAsync(employee.EmployeeId);
            IsBusy = false;
            await new EmployeeDebtPaymentHistoryDialog(employee, payments).ShowDialog(owner);
        }
        catch (Exception exception) when (IsApiFailure(exception)) { ShowError("Payment history could not be loaded", FailureMessage(exception)); }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task ViewPurchasesAsync(EmployeeBalanceResponse? employee)
    {
        if (employee is null || IsBusy || MainWindow() is not { } owner) return;
        IsBusy = true;
        try
        {
            var purchases = await _api.GetEmployeeOwedPurchasesAsync(employee.EmployeeId);
            IsBusy = false;
            await new EmployeeOwedPurchasesDialog(employee, purchases).ShowDialog(owner);
        }
        catch (Exception exception) when (IsApiFailure(exception)) { ShowError("Owed purchases could not be loaded", FailureMessage(exception)); }
        finally { IsBusy = false; }
    }

    private void ApplyFilter()
    {
        var search = SearchText.Trim();
        Balances = (search.Length == 0 ? _allBalances : _allBalances.Where(employee =>
            employee.EmployeeNumber.Contains(search, StringComparison.OrdinalIgnoreCase) ||
            employee.EmployeeName.Contains(search, StringComparison.OrdinalIgnoreCase))).ToArray();
        OnPropertyChanged(nameof(OutstandingDisplay));
        OnPropertyChanged(nameof(EmployeesWithBalanceDisplay));
        OnPropertyChanged(nameof(RepaymentsDisplay));
        OnPropertyChanged(nameof(FullyPaidDisplay));
        OnPropertyChanged(nameof(LargestBalanceDisplay));
    }

    private void ShowError(string title, string message) { StatusMessage = message; _notifications.ShowError(title, message); }
    private static Avalonia.Controls.Window? MainWindow() => (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
    private static bool IsApiFailure(Exception exception) => exception is ApiClientException or HttpRequestException or TaskCanceledException;
    private static string FailureMessage(Exception exception) => UserFacingErrors.Get(exception, "Employee balances could not be loaded. Please try again.");
}
