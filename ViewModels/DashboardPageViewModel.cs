using AvaloniaApp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AvaloniaApp.ViewModels;

public partial class DashboardPageViewModel : ObservableObject
{
    private readonly StoreApiClient _api;
    private readonly INotificationService _notifications;

    [ObservableProperty]
    private IReadOnlyList<InventoryReportProductResponse> _attentionItems = [];

    [ObservableProperty]
    private IReadOnlyList<ReportSaleResponse> _recentSales = [];

    [ObservableProperty] private decimal _todaySales;
    [ObservableProperty] private int _todayTransactions;
    [ObservableProperty] private int _displayUnits;
    [ObservableProperty] private int _bodegaUnits;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string _statusMessage = "Loading store overview...";
    [ObservableProperty] private string? _errorMessage;

    public string TodaySalesDisplay => $"₱{TodaySales:N2}";
    public int ShelfUnits => DisplayUnits;
    public int AttentionCount => AttentionItems.Count;

    public DashboardPageViewModel(StoreApiClient api, INotificationService notifications)
    {
        _api = api;
        _notifications = notifications;
        _ = RefreshAsync();
    }

    [RelayCommand]
    public async Task RefreshAsync()
    {
        if (IsLoading) return;
        IsLoading = true;
        ErrorMessage = null;
        StatusMessage = "Loading store overview...";
        try
        {
            var dashboard = await _api.GetDashboardAsync();
            TodaySales = dashboard.TodaySales;
            TodayTransactions = dashboard.TodayTransactions;
            DisplayUnits = dashboard.DisplayUnits;
            BodegaUnits = dashboard.BodegaUnits;
            AttentionItems = dashboard.AttentionItems;
            RecentSales = dashboard.RecentSales;
            StatusMessage = "Store overview is up to date.";
            OnPropertyChanged(nameof(TodaySalesDisplay));
            OnPropertyChanged(nameof(ShelfUnits));
            OnPropertyChanged(nameof(AttentionCount));
        }
        catch (Exception exception) when (exception is ApiClientException or HttpRequestException or TaskCanceledException)
        {
            TodaySales = 0;
            TodayTransactions = 0;
            DisplayUnits = 0;
            BodegaUnits = 0;
            AttentionItems = [];
            RecentSales = [];
            ErrorMessage = exception is TaskCanceledException ? "The dashboard request timed out." : exception.Message;
            StatusMessage = ErrorMessage;
            _notifications.ShowError("Store overview could not be loaded", ErrorMessage);
            OnPropertyChanged(nameof(TodaySalesDisplay));
            OnPropertyChanged(nameof(ShelfUnits));
            OnPropertyChanged(nameof(AttentionCount));
        }
        finally
        {
            IsLoading = false;
        }
    }
}
