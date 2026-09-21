using AvaloniaApp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AvaloniaApp.ViewModels;

public sealed record CashierSalesRange(string Label)
{
    public override string ToString() => Label;
}

public partial class CashierSalesViewModel : ObservableObject
{
    private readonly StoreApiClient _api;
    [ObservableProperty] private SalesReportResponse? _report;
    [ObservableProperty] private ReportSaleResponse? _selectedSale;
    [ObservableProperty] private CashierSalesRange _selectedRange = new("Today");
    [ObservableProperty] private DateTimeOffset? _fromDate;
    [ObservableProperty] private DateTimeOffset? _toDate;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string _statusMessage = "Loading your sales...";
    public IReadOnlyList<CashierSalesRange> Ranges { get; } = [new("Today"), new("Yesterday"), new("This week"), new("This month"), new("All time"), new("Custom")];
    public IReadOnlyList<ReportSaleResponse> Sales => Report?.Sales ?? [];
    public string TotalDisplay => $"₱{Report?.Summary.GrossSales ?? 0:N2}";
    public string TransactionsDisplay => (Report?.Summary.Transactions ?? 0).ToString("N0");
    public string UnitsDisplay => (Report?.Summary.UnitsSold ?? 0).ToString("N0");
    public string CashDisplay => $"₱{Sales.Where(s => s.PaymentMethod == ApiPaymentMethod.Cash).Sum(s => s.Total):N2}";
    public string GCashDisplay => $"₱{Sales.Where(s => s.PaymentMethod == ApiPaymentMethod.GCash).Sum(s => s.Total):N2}";
    public string OwedDisplay => $"₱{Sales.Where(s => s.PaymentMethod == ApiPaymentMethod.EmployeeOwed).Sum(s => s.Total):N2}";

    public CashierSalesViewModel(StoreApiClient api) { _api = api; _ = RefreshAsync(); }
    partial void OnSelectedRangeChanged(CashierSalesRange value) => _ = RefreshAsync();
    partial void OnFromDateChanged(DateTimeOffset? value) { if (SelectedRange.Label == "Custom") _ = RefreshAsync(); }
    partial void OnToDateChanged(DateTimeOffset? value) { if (SelectedRange.Label == "Custom") _ = RefreshAsync(); }
    [RelayCommand]
    public async Task RefreshAsync()
    {
        if (IsLoading) return;
        IsLoading = true;
        try
        {
            var (from, to) = Range(value: SelectedRange.Label);
            Report = await _api.GetMySalesReportAsync(from, to);
            StatusMessage = $"Showing {SelectedRange.Label.ToLowerInvariant()} sales.";
            Notify();
        }
        catch (Exception ex) when (ex is ApiClientException or HttpRequestException or TaskCanceledException)
        {
            Report = null; StatusMessage = ex.Message; Notify();
        }
        finally { IsLoading = false; }
    }
    private (DateTimeOffset?, DateTimeOffset?) Range(string value)
    {
        var today = StoreDateTime.StoreToday;
        return value switch
        {
            "Yesterday" => (StoreDateTime.AtStoreMidnight(today.AddDays(-1)).ToUniversalTime(), StoreDateTime.AtStoreMidnight(today).ToUniversalTime()),
            "This week" => (StoreDateTime.AtStoreMidnight(today.AddDays(-((int)today.DayOfWeek + 6) % 7)).ToUniversalTime(), StoreDateTime.AtStoreMidnight(today.AddDays(1)).ToUniversalTime()),
            "This month" => (StoreDateTime.AtStoreMidnight(new DateTime(today.Year, today.Month, 1)).ToUniversalTime(), StoreDateTime.AtStoreMidnight(today.AddDays(1)).ToUniversalTime()),
            "All time" => (null, null),
            "Custom" => (FromDate?.ToUniversalTime(), ToDate?.Date.AddDays(1) is { } end ? new DateTimeOffset(end, TimeSpan.Zero) : null),
            _ => (StoreDateTime.AtStoreMidnight(today).ToUniversalTime(), StoreDateTime.AtStoreMidnight(today.AddDays(1)).ToUniversalTime())
        };
    }
    private void Notify()
    {
        OnPropertyChanged(nameof(Sales)); OnPropertyChanged(nameof(TotalDisplay)); OnPropertyChanged(nameof(TransactionsDisplay));
        OnPropertyChanged(nameof(UnitsDisplay)); OnPropertyChanged(nameof(CashDisplay)); OnPropertyChanged(nameof(GCashDisplay)); OnPropertyChanged(nameof(OwedDisplay));
    }
}
