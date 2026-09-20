using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using AvaloniaApp.Services;
using AvaloniaApp.Views.Dialogs;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AvaloniaApp.ViewModels;

public partial class CashierShiftViewModel : ObservableObject, IDisposable
{
    private readonly INotificationService _notifications;
    private readonly Func<bool> _hasPendingSale;
    private readonly StoreApiClient _api;

    [ObservableProperty] private string _statusMessage = "Checking your cashier assignment...";
    [ObservableProperty] private ApiCashAdjustmentType _selectedAdjustmentType = ApiCashAdjustmentType.CashRefund;
    [ObservableProperty] private decimal? _adjustmentAmount;
    [ObservableProperty] private string _adjustmentNote = "";
    [ObservableProperty] private string _adjustmentReference = "";
    [ObservableProperty] private IReadOnlyList<CashierCashAdjustmentResponse> _adjustments = [];

    public CashierShiftState Shift { get; }
    public bool CanClockIn => Shift.CanClockIn;
    public bool HasBlockingPendingAdjustments => Adjustments.Any(item =>
        item.Status == ApiCashAdjustmentStatus.Pending && item.Type is ApiCashAdjustmentType.CashRefund or ApiCashAdjustmentType.CashPayout);
    public bool CanClockOut => Shift.IsClockedIn && !Shift.IsLoading && !HasBlockingPendingAdjustments;
    public bool HasAssignment => Shift.Assignment is not null;
    public bool HasOpenSession => Shift.OpenSession is not null;
    public bool HasBlockReason => !string.IsNullOrWhiteSpace(Shift.ClockStatus?.BlockReason);
    public string AssignmentWindow => Shift.Assignment?.ScheduleDisplay ?? "No assignment resolved for the current store time.";
    public string ActualWindow => Shift.OpenSession?.ActualDisplay ?? "Not clocked in";
    public string OpeningFloatDisplay => Shift.OpenSession is { } session ? $"₱{session.OpeningCashFloat:N2}" : "-";
    public string BlockReason => Shift.ClockStatus?.BlockReason ?? "";
    public IReadOnlyList<ApiCashAdjustmentType> AdjustmentTypes { get; } = Enum.GetValues<ApiCashAdjustmentType>();
    public bool CanRequestAdjustment => HasOpenSession && !Shift.IsLoading && AdjustmentAmount is > 0 && !string.IsNullOrWhiteSpace(AdjustmentNote);
    public string AdjustmentValidationMessage => !HasOpenSession ? "Clock in before recording a cash adjustment request."
        : AdjustmentAmount is null or <= 0 ? "Enter an amount greater than zero."
        : string.IsNullOrWhiteSpace(AdjustmentNote) ? "A note is required for every request."
        : AdjustmentNote.Length > 1000 ? "The note cannot exceed 1,000 characters."
        : AdjustmentReference.Length > 160 ? "The reference cannot exceed 160 characters." : "";

    public CashierShiftViewModel(CashierShiftState shift, StoreApiClient api, INotificationService notifications, Func<bool>? hasPendingSale = null)
    {
        Shift = shift;
        _api = api;
        _notifications = notifications;
        _hasPendingSale = hasPendingSale ?? (() => false);
        Shift.PropertyChanged += OnShiftChanged;
        UpdateStatus();
        if (HasOpenSession) _ = LoadAdjustmentsAsync();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        StatusMessage = "Refreshing cashier status...";
        if (await Shift.RefreshAsync())
        {
            UpdateStatus();
            await LoadAdjustmentsAsync();
        }
        else StatusMessage = Shift.ErrorMessage ?? "Unable to refresh cashier status.";
    }

    [RelayCommand(CanExecute = nameof(CanClockIn))]
    private async Task ClockInAsync()
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime { MainWindow: { } owner }) return;
        var openingFloat = Shift.PendingClockInOpeningFloat;
        if (openingFloat is null)
        {
            openingFloat = await new OpeningFloatDialog().ShowDialog<decimal?>(owner);
            if (openingFloat is null) return;
        }
        try
        {
            var session = await Shift.ClockInAsync(openingFloat.Value);
            StatusMessage = $"Clocked in to {session.ShiftName} at {StoreDateTime.FormatUtc(session.ClockedInAtUtc)}.";
            _notifications.ShowSuccess("Cashier work period started", StatusMessage);
            await LoadAdjustmentsAsync();
        }
        catch (Exception exception) when (IsApiFailure(exception))
        {
            if (exception is ApiClientException) await Shift.RefreshAsync();
            StatusMessage = Shift.PendingClockInOpeningFloat is { } pending
                ? $"{exception.Message} Retry will reuse the original ₱{pending:N2} starting cash and idempotency key."
                : exception.Message;
            _notifications.ShowError("Cashier work period could not be started", FailureMessage(exception));
        }
    }

    [RelayCommand(CanExecute = nameof(CanRequestAdjustment))]
    private async Task RequestAdjustmentAsync()
    {
        if (Shift.OpenSession is not { } session || AdjustmentAmount is not > 0 || string.IsNullOrWhiteSpace(AdjustmentNote)) return;
        try
        {
            await _api.CreateCashAdjustmentAsync(session.Id, new CreateCashAdjustmentRequest(
                SelectedAdjustmentType, AdjustmentAmount.Value, AdjustmentNote.Trim(), NullIfWhiteSpace(AdjustmentReference)));
            AdjustmentAmount = null;
            AdjustmentNote = "";
            AdjustmentReference = "";
            StatusMessage = "Cash adjustment request sent for Admin review.";
            _notifications.ShowSuccess("Request submitted successfully", StatusMessage);
            await LoadAdjustmentsAsync();
        }
        catch (Exception exception) when (IsApiFailure(exception))
        {
            if (exception is ApiClientException)
            {
                await Shift.RefreshAsync();
                await LoadAdjustmentsAsync();
            }
            StatusMessage = exception.Message;
            _notifications.ShowError("Request could not be submitted", FailureMessage(exception));
        }
    }

    private async Task LoadAdjustmentsAsync()
    {
        if (Shift.OpenSession is not { } session)
        {
            Adjustments = [];
            return;
        }
        try
        {
            Adjustments = await _api.GetCashAdjustmentsAsync(session.Id);
            OnPropertyChanged(nameof(HasBlockingPendingAdjustments));
            OnPropertyChanged(nameof(CanClockOut));
            ClockOutCommand.NotifyCanExecuteChanged();
        }
        catch (Exception exception) when (IsApiFailure(exception))
        {
            StatusMessage = $"Shift is active, but cash adjustments could not be loaded: {exception.Message}";
        }
    }

    [RelayCommand(CanExecute = nameof(CanClockOut))]
    private async Task ClockOutAsync()
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime { MainWindow: { } owner }) return;
        if (_hasPendingSale())
        {
            var warning = new ConfirmDialog();
            warning.SetInformation("Current sale must be resolved", "The POS cart has unsaved items. Complete or clear that sale before clocking out so it is not lost or assigned incorrectly.");
            await warning.ShowDialog(owner);
            return;
        }
        var dialog = new ConfirmDialog();
        dialog.SetConfirmation("Clock out and close this shift?", "Clock-out is final, prevents additional sales on this session, and starts expected cash-remittance calculation. Remain clocked in during ordinary breaks.", "Clock out");
        await dialog.ShowDialog(owner);
        if (!dialog.Confirmed) return;
        try
        {
            var session = await Shift.ClockOutAsync();
            StatusMessage = $"Clocked out at {StoreDateTime.FormatUtc(session.ClockedOutAtUtc ?? DateTime.UtcNow)}. Remittance is pending Admin review.";
            _notifications.ShowSuccess("Cashier work period ended", StatusMessage);
            await new CashierShiftSummaryDialog(session).ShowDialog(owner);
        }
        catch (Exception exception) when (IsApiFailure(exception))
        {
            if (exception is ApiClientException)
            {
                await Shift.RefreshAsync();
                await LoadAdjustmentsAsync();
            }
            StatusMessage = exception.Message;
            _notifications.ShowError("Cashier work period could not be ended", FailureMessage(exception));
        }
    }

    private void OnShiftChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        OnPropertyChanged(nameof(CanClockIn));
        OnPropertyChanged(nameof(CanClockOut));
        OnPropertyChanged(nameof(HasAssignment));
        OnPropertyChanged(nameof(HasOpenSession));
        OnPropertyChanged(nameof(HasBlockReason));
        OnPropertyChanged(nameof(AssignmentWindow));
        OnPropertyChanged(nameof(ActualWindow));
        OnPropertyChanged(nameof(OpeningFloatDisplay));
        OnPropertyChanged(nameof(BlockReason));
        OnPropertyChanged(nameof(CanRequestAdjustment));
        OnPropertyChanged(nameof(AdjustmentValidationMessage));
        RequestAdjustmentCommand.NotifyCanExecuteChanged();
        ClockInCommand.NotifyCanExecuteChanged();
        ClockOutCommand.NotifyCanExecuteChanged();
        if (e.PropertyName == nameof(CashierShiftState.ErrorMessage) && Shift.ErrorMessage is not null)
            StatusMessage = Shift.ErrorMessage;
    }

    private void UpdateStatus() => StatusMessage = Shift.ErrorMessage ?? Shift.StatusDisplay;
    partial void OnAdjustmentAmountChanged(decimal? value) => NotifyAdjustmentState();
    partial void OnAdjustmentNoteChanged(string value) => NotifyAdjustmentState();
    partial void OnAdjustmentReferenceChanged(string value) => NotifyAdjustmentState();
    partial void OnAdjustmentsChanged(IReadOnlyList<CashierCashAdjustmentResponse> value)
    {
        OnPropertyChanged(nameof(HasBlockingPendingAdjustments));
        OnPropertyChanged(nameof(CanClockOut));
        ClockOutCommand.NotifyCanExecuteChanged();
    }
    private void NotifyAdjustmentState()
    {
        OnPropertyChanged(nameof(CanRequestAdjustment));
        OnPropertyChanged(nameof(AdjustmentValidationMessage));
        RequestAdjustmentCommand.NotifyCanExecuteChanged();
    }
    private static string? NullIfWhiteSpace(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static bool IsApiFailure(Exception exception) => exception is ApiClientException or HttpRequestException or TaskCanceledException;
    private static string FailureMessage(Exception exception) => exception switch
    {
        HttpRequestException => "We could not connect to the store.",
        TaskCanceledException => "The store took too long to respond. Please try again.",
        _ => exception.Message
    };

    public void Dispose() => Shift.PropertyChanged -= OnShiftChanged;
}
