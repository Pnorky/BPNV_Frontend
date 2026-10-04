using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using AvaloniaApp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AvaloniaApp.ViewModels;

public partial class StatementOfAccountViewModel : ObservableObject
{
    private readonly StoreApiClient _api;
    private readonly INotificationService _notifications;
    [ObservableProperty] private IReadOnlyList<CustomerResponse> _customers = [];
    [ObservableProperty] private CustomerResponse? _selectedCustomer;
    [ObservableProperty] private DateTimeOffset? _fromDate = new(StoreDateTime.StoreToday.AddMonths(-1));
    [ObservableProperty] private DateTimeOffset? _toDate = new(StoreDateTime.StoreToday);
    [ObservableProperty] private CustomerStatementResponse? _statement;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _statusMessage = "Select a customer and statement period.";

    public bool HasStatement => Statement is not null;
    public string PreviousBalanceDisplay => $"₱{Statement?.PreviousBalance ?? 0:N2}";
    public string CurrentChargesDisplay => $"₱{Statement?.TotalCurrentCharges ?? 0:N2}";
    public string PeriodPaymentsDisplay => $"₱{Statement?.TotalPeriodPayments ?? 0:N2}";
    public string TotalBalanceDisplay => $"₱{Statement?.TotalBalance ?? 0:N2}";

    public StatementOfAccountViewModel(StoreApiClient api, INotificationService notifications)
    {
        _api = api;
        _notifications = notifications;
        _ = LoadCustomersAsync();
    }

    partial void OnSelectedCustomerChanged(CustomerResponse? value) => Invalidate();
    partial void OnFromDateChanged(DateTimeOffset? value) => Invalidate();
    partial void OnToDateChanged(DateTimeOffset? value) => Invalidate();
    partial void OnStatementChanged(CustomerStatementResponse? value)
    {
        OnPropertyChanged(nameof(HasStatement));
        OnPropertyChanged(nameof(PreviousBalanceDisplay));
        OnPropertyChanged(nameof(CurrentChargesDisplay));
        OnPropertyChanged(nameof(PeriodPaymentsDisplay));
        OnPropertyChanged(nameof(TotalBalanceDisplay));
    }

    [RelayCommand]
    private async Task PreviewAsync()
    {
        if (SelectedCustomer is null || !FromDate.HasValue || !ToDate.HasValue)
        {
            _notifications.ShowError("SOA preview unavailable", "Select a customer and date range."); return;
        }
        var from = DateOnly.FromDateTime(FromDate.Value.Date);
        var to = DateOnly.FromDateTime(ToDate.Value.Date);
        if (to < from) { _notifications.ShowError("Invalid period", "The end date cannot be before the start date."); return; }
        IsBusy = true;
        try
        {
            Statement = await _api.GetCustomerStatementAsync(SelectedCustomer.Id, from, to);
            StatusMessage = $"Preview loaded with {Statement.Charges.Count} charge line{(Statement.Charges.Count == 1 ? "" : "s")}.";
        }
        catch (Exception exception) when (IsApiFailure(exception))
        {
            StatusMessage = UserFacingErrors.Get(exception, "The SOA preview could not be loaded.");
            _notifications.ShowError("SOA unavailable", StatusMessage);
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task ExportPdfAsync()
    {
        if (Statement is null || (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow is not { } owner) return;
        var file = await owner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save statement of account",
            SuggestedFileName = BuildSuggestedFileName(Statement),
            FileTypeChoices = [new FilePickerFileType("PDF document") { Patterns = ["*.pdf"] }]
        });
        if (file is null) return;
        await using var output = await file.OpenWriteAsync();
        if (output.CanSeek) output.SetLength(0);
        StatementOfAccountPdfService.Export(Statement, output);
        StatusMessage = "Statement of account exported successfully.";
        _notifications.ShowSuccess("SOA exported", StatusMessage);
    }

    private async Task LoadCustomersAsync()
    {
        try { Customers = (await _api.GetCustomersAsync(includeInactive: true, pageSize: 100)).Items.ToArray(); }
        catch (Exception exception) when (IsApiFailure(exception)) { StatusMessage = UserFacingErrors.Get(exception, "Customers could not be loaded."); }
    }

    private void Invalidate() { Statement = null; StatusMessage = "Select Preview to load authoritative account totals."; }
    internal static string BuildSuggestedFileName(CustomerStatementResponse statement)
    {
        var invalidCharacters = Path.GetInvalidFileNameChars();
        var customerName = string.Concat(statement.Customer.Name.Trim().Select(character =>
            invalidCharacters.Contains(character) ? '-' : character)).Trim(' ', '.');
        if (customerName.Length == 0) customerName = "Customer";
        return $"BPNV_{customerName}_{statement.ToDate:MMMMyyyy}.pdf";
    }
    private static bool IsApiFailure(Exception exception) => exception is ApiClientException or HttpRequestException or TaskCanceledException;
}
