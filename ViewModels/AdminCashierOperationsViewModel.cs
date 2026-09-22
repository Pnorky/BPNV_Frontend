using AvaloniaApp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AvaloniaApp.ViewModels;

public sealed record CashierShiftStatusFilter(string Label, ApiCashierShiftSessionStatus? Value)
{
    public override string ToString() => Label;
}

public sealed record TransactionDateRangeOption(string Label)
{
    public override string ToString() => Label;
}

public partial class AdminCashierOperationsViewModel : ObservableObject, IDisposable
{
    public event Action<CashierShiftSessionDetailResponse>? SessionDetailLoaded;
    private static readonly CashierShiftStatusFilter AllStatuses = new("All statuses", null);
    private readonly StoreApiClient _api;
    private readonly INotificationService _notifications;
    private CancellationTokenSource? _loadCancellation;
    private CancellationTokenSource? _detailCancellation;
    private AppliedFilters _appliedFilters;
    private AdministrativeClockOutRequest? _pendingAdministrativeClockOut;
    private bool _suppressSelectionLoad;

    public AdminCashierOperationsViewModel(StoreApiClient api, INotificationService notifications)
    {
        _api = api;
        _notifications = notifications;
        var today = StoreDateTime.StoreToday;
        _fromDate = StoreDateTime.AtStoreMidnight(today);
        _toDate = StoreDateTime.AtStoreMidnight(today);
        _transactionFromDate = StoreDateTime.AtStoreMidnight(today);
        _transactionToDate = StoreDateTime.AtStoreMidnight(today.AddDays(1));
        _transactionFromDate = _fromDate;
        _transactionToDate = _toDate;
        _selectedStatusFilter = AllStatuses;
        _appliedFilters = new(ToDateOnly(_fromDate.Value), ToDateOnly(_toDate.Value).AddDays(1), null, null, null);
        _ = LoadAsync();
    }

    public IReadOnlyList<CashierShiftStatusFilter> StatusFilters { get; } =
    [
        AllStatuses,
        new("Open", ApiCashierShiftSessionStatus.Open),
        new("Pending remittance", ApiCashierShiftSessionStatus.ClosedPendingRemittance),
        new("Reconciled", ApiCashierShiftSessionStatus.Reconciled)
    ];

    public IReadOnlyList<int> PageSizeOptions { get; } = [10, 20, 50];
    public IReadOnlyList<TransactionDateRangeOption> HistoryDateRanges { get; } =
        [new("Today"), new("Yesterday"), new("This week"), new("This month"), new("All time"), new("Custom")];
    public IReadOnlyList<TransactionDateRangeOption> TransactionDateRanges { get; } =
        [new("Today"), new("Yesterday"), new("This week"), new("This month"), new("All time"), new("Custom")];

    [ObservableProperty] private DateTimeOffset? _fromDate;
    [ObservableProperty] private DateTimeOffset? _toDate;
    [ObservableProperty] private TransactionDateRangeOption _selectedHistoryDateRange = new("Today");
    [ObservableProperty] private CashierShiftStatusFilter _selectedStatusFilter;
    [ObservableProperty] private IReadOnlyList<UserResponse> _cashiers = [];
    [ObservableProperty] private UserResponse? _selectedCashier;
    [ObservableProperty] private IReadOnlyList<ShiftDefinitionResponse> _shiftDefinitions = [];
    [ObservableProperty] private ShiftDefinitionResponse? _selectedShiftDefinition;
    [ObservableProperty] private IReadOnlyList<CashierShiftSessionResponse> _sessions = [];
    [ObservableProperty] private CashierShiftSessionResponse? _selectedSession;
    [ObservableProperty] private CashierShiftSessionDetailResponse? _detail;
    [ObservableProperty] private CashierShiftReportResponse? _report;
    [ObservableProperty] private int _page = 1;
    [ObservableProperty] private int _pageSize = 20;
    [ObservableProperty] private int _totalCount;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _isDetailLoading;
    [ObservableProperty] private bool _isMutating;
    [ObservableProperty] private string? _listError;
    [ObservableProperty] private string? _detailError;
    [ObservableProperty] private string _statusMessage = "Loading cashier shift history...";
    [ObservableProperty] private DateTimeOffset? _transactionFromDate;
    [ObservableProperty] private DateTimeOffset? _transactionToDate;
    [ObservableProperty] private TransactionDateRangeOption _selectedTransactionDateRange = new("Today");
    [ObservableProperty] private UserResponse? _transactionCashier;
    [ObservableProperty] private ShiftDefinitionResponse? _transactionShift;
    [ObservableProperty] private IReadOnlyList<ReportSaleResponse> _transactionSales = [];
    [ObservableProperty] private ReportSaleResponse? _selectedTransaction;
    [ObservableProperty] private string _transactionStatusMessage = "Loading transactions...";
    private SalesReportResponse? _transactionReport;

    [ObservableProperty] private decimal? _actualRemittance;
    [ObservableProperty] private bool _cashFloatReturned;
    [ObservableProperty] private string _remittanceNote = "";
    [ObservableProperty] private string _administrativeClockOutReason = "";
    [ObservableProperty] private string _adjustmentReviewNote = "";
    [ObservableProperty] private bool _correctAdjustmentToApproved = true;
    [ObservableProperty] private string _adjustmentCorrectionReason = "";
    [ObservableProperty] private decimal? _correctedActualRemittance;
    [ObservableProperty] private bool _correctedCashFloatReturned;
    [ObservableProperty] private string _correctionReason = "";

    public bool HasSessions => Sessions.Count > 0;
    public bool HasDetail => Detail is not null;
    public bool ShowDetailPlaceholder => Detail is null && !IsDetailLoading;
    public bool HasSales => Detail?.Sales.Count > 0;
    public bool HasAdjustments => Detail?.CashAdjustments.Count > 0;
    public bool HasCorrections => Detail?.Corrections.Count > 0;
    public bool HasReport => Report is not null;
    public bool IsHistoryCustomDateRange => SelectedHistoryDateRange.Label == "Custom";
    public bool IsTransactionCustomDateRange => SelectedTransactionDateRange.Label == "Custom";
    public bool CanShowAdministrativeClockOut => Detail?.Session.Status == ApiCashierShiftSessionStatus.Open;
    public bool HasBlockingPendingAdjustments => Detail?.CashAdjustments.Any(item =>
        item.Status == ApiCashAdjustmentStatus.Pending && item.Type is ApiCashAdjustmentType.CashRefund or ApiCashAdjustmentType.CashPayout) == true;
    public bool CanAdministrativeClockOutAction => CanAdministrativeClockOut();
    public bool CanShowRemittanceForm => Detail?.Session.Status == ApiCashierShiftSessionStatus.ClosedPendingRemittance;
    public bool CanShowCorrectionForm => Detail?.Session.Status == ApiCashierShiftSessionStatus.Reconciled;
    public decimal? ProvisionalVariance => ActualRemittance.HasValue && Detail?.Session.ExpectedRemittance is { } expected
        ? ActualRemittance.Value - expected
        : null;
    public string ProvisionalVarianceDisplay => CashierShiftFormatting.SignedMoney(ProvisionalVariance);
    public string ProvisionalVarianceState => ProvisionalVariance switch
    {
        < 0 => "Shortage",
        > 0 => "Overage",
        0 => "Balanced",
        _ => "Pending amount"
    };
    public string? RemittanceValidationMessage
    {
        get
        {
            if (!CanShowRemittanceForm) return null;
            if (!ActualRemittance.HasValue) return "Enter the actual sales cash remittance.";
            if (ActualRemittance.Value < 0) return "Actual remittance cannot be negative.";
            if (Detail?.Session.ExpectedRemittance is null) return "The expected remittance is not available yet.";
            if ((!CashFloatReturned || ProvisionalVariance != 0) && string.IsNullOrWhiteSpace(RemittanceNote))
                return "A note is required for a non-zero variance or unreturned starting cash.";
            if (RemittanceNote.Length > 1000) return "The remittance note cannot exceed 1,000 characters.";
            return null;
        }
    }
    public string? AdministrativeClockOutValidationMessage =>
        !CanShowAdministrativeClockOut ? null
        : HasBlockingPendingAdjustments ? "Review pending Cash refund and payout requests before closing this shift."
         : string.IsNullOrWhiteSpace(AdministrativeClockOutReason) ? "A reason is required to close the shift for the cashier."
        : AdministrativeClockOutReason.Length > 1000 ? "The reason cannot exceed 1,000 characters." : null;
    public string? CorrectionValidationMessage
    {
        get
        {
            if (!CanShowCorrectionForm) return null;
            if (!CorrectedActualRemittance.HasValue) return "Enter the corrected actual remittance.";
            if (CorrectedActualRemittance.Value < 0) return "Corrected actual remittance cannot be negative.";
            if (string.IsNullOrWhiteSpace(CorrectionReason)) return "An approved cash correction reason is required.";
            if (CorrectionReason.Length > 1000) return "The correction reason cannot exceed 1,000 characters.";
            return null;
        }
    }
    public string PageSummary
    {
        get
        {
            if (TotalCount == 0) return "0 of 0";
            var start = (Page - 1) * PageSize + 1;
            return $"{start:N0}-{Math.Min(Page * PageSize, TotalCount):N0} of {TotalCount:N0}";
        }
    }
    public string ReportSessionsDisplay => Report?.Summary.Sessions.ToString("N0") ?? "-";
    public string ReportTotalSalesDisplay => CashierShiftFormatting.Money(Report?.Summary.TotalSales);
    public string ReportCashSalesDisplay => CashierShiftFormatting.Money(Report?.Summary.CashSales);
    public string ReportGCashSalesDisplay => CashierShiftFormatting.Money(Report?.Summary.GCashSales);
    public string ReportExpectedDisplay => CashierShiftFormatting.Money(Report?.Summary.ExpectedRemittance);
    public string ReportActualDisplay => CashierShiftFormatting.Money(Report?.Summary.ActualRemittance);
    public string ReportVarianceDisplay => CashierShiftFormatting.SignedMoney(Report?.Summary.Variance);

    private bool CanPreviousPage() => !IsLoading && Page > 1;
    private bool CanNextPage() => !IsLoading && Page * PageSize < TotalCount;
    private bool CanRecordRemittance() => !IsMutating && RemittanceValidationMessage is null && CanShowRemittanceForm;
    private bool CanAdministrativeClockOut() => !IsMutating && CanShowAdministrativeClockOut && AdministrativeClockOutValidationMessage is null;
    private bool CanCorrectRemittance() => !IsMutating && CorrectionValidationMessage is null && CanShowCorrectionForm;
    private bool CanReviewAdjustment(CashierCashAdjustmentResponse? adjustment) =>
        !IsMutating && AdjustmentReviewNote.Length <= 1000 && adjustment?.Status == ApiCashAdjustmentStatus.Pending &&
        (Detail?.Session.Status == ApiCashierShiftSessionStatus.Open || adjustment.Type == ApiCashAdjustmentType.StoreExpense);
    private bool CanCorrectAdjustment(CashierCashAdjustmentResponse? adjustment) =>
        !IsMutating && adjustment is { Status: not ApiCashAdjustmentStatus.Pending } &&
        !string.IsNullOrWhiteSpace(AdjustmentCorrectionReason) && AdjustmentCorrectionReason.Length <= 1000 &&
        ((adjustment.Status == ApiCashAdjustmentStatus.Approved) != CorrectAdjustmentToApproved);

    [RelayCommand]
    public async Task LoadAsync()
    {
        if (IsLoading) return;
        await LoadPageAndReportAsync();
    }

    [RelayCommand]
    public async Task ApplyFiltersAsync()
    {
        if (IsLoading) return;
        if (FromDate is null || ToDate is null)
        {
            ShowError("Invalid shift filters", "Select both a start date and an end date.");
            return;
        }
        if (FromDate.Value.Date > ToDate.Value.Date)
        {
            ShowError("Invalid shift filters", "The start date must be earlier than or equal to the end date.");
            return;
        }

        var dayCount = (ToDateOnly(ToDate.Value).DayNumber - ToDateOnly(FromDate.Value).DayNumber) + 1;
        if (dayCount is < 1 or > 366)
        {
            ShowError("Invalid shift filters", "Select a report range between 1 and 366 business days.");
            return;
        }

        _appliedFilters = new(ToDateOnly(FromDate.Value), ToDateOnly(ToDate.Value).AddDays(1),
            SelectedCashier?.Id, SelectedShiftDefinition?.Id, SelectedStatusFilter.Value);
        Page = 1;
        await LoadPageAndReportAsync();
    }

    [RelayCommand]
    public Task RefreshAsync() => IsLoading ? Task.CompletedTask : LoadPageAndReportAsync();

    [RelayCommand]
    public async Task ClearFiltersAsync()
    {
        SelectedCashier = null;
        SelectedShiftDefinition = null;
        SelectedStatusFilter = AllStatuses;
        SelectedHistoryDateRange = HistoryDateRanges[0];
        await ApplyFiltersAsync();
    }

    [RelayCommand]
    public Task ApplyTransactionFiltersAsync() { ApplyTransactionFilter(); return Task.CompletedTask; }

    [RelayCommand]
    public Task ClearTransactionFiltersAsync()
    {
        TransactionCashier = null;
        TransactionShift = null;
        SelectedTransactionDateRange = TransactionDateRanges[0];
        ApplyTransactionFilter();
        return Task.CompletedTask;
    }

    [RelayCommand]
    public async Task RefreshTransactionsAsync()
    {
        if (_transactionReport is null) await LoadPageAndReportAsync();
        else ApplyTransactionFilter();
    }

    [RelayCommand(CanExecute = nameof(CanPreviousPage))]
    public async Task PreviousPageAsync()
    {
        Page--;
        await LoadPageAndReportAsync();
    }

    [RelayCommand(CanExecute = nameof(CanNextPage))]
    public async Task NextPageAsync()
    {
        Page++;
        await LoadPageAndReportAsync();
    }

    [RelayCommand]
    public void SelectSession(CashierShiftSessionResponse? session) => SelectedSession = session;

    public async Task OpenSessionAsync(Guid sessionId)
    {
        _detailCancellation?.Cancel();
        IsDetailLoading = true;
        DetailError = null;
        try
        {
            var detail = await _api.GetCashierShiftSessionAsync(sessionId);
            _suppressSelectionLoad = true;
            SelectedSession = detail.Session;
            _suppressSelectionLoad = false;
            Detail = detail;
            InitializeForms(detail.Session);
            SessionDetailLoaded?.Invoke(detail);
        }
        catch (Exception exception) when (IsApiFailure(exception))
        {
            DetailError = FailureMessage(exception);
                _notifications.ShowError("Work period details could not be loaded", DetailError);
        }
        finally
        {
            _suppressSelectionLoad = false;
            IsDetailLoading = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanRecordRemittance))]
    public async Task RecordRemittanceAsync()
    {
        if (Detail is null || RemittanceValidationMessage is not null || !ActualRemittance.HasValue) return;
        var sessionId = Detail.Session.Id;
        IsMutating = true;
        try
        {
            await _api.RecordRemittanceAsync(sessionId, new RecordRemittanceRequest(
                ActualRemittance.Value,
                CashFloatReturned,
                NullIfWhiteSpace(RemittanceNote)));
            _notifications.ShowSuccess("Remittance recorded", "The cashier work period cash was reviewed.");
            StatusMessage = "Remittance recorded and work period details refreshed.";
            await ReloadAfterMutationAsync(sessionId, resetForms: true);
        }
        catch (Exception exception) when (IsApiFailure(exception))
        {
            ShowError("Remittance not recorded", FailureMessage(exception));
        }
        finally
        {
            IsMutating = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanAdministrativeClockOut))]
    public async Task AdministrativeClockOutAsync()
    {
        if (Detail is null || AdministrativeClockOutValidationMessage is not null) return;
        var sessionId = Detail.Session.Id;
        var request = _pendingAdministrativeClockOut ??= new AdministrativeClockOutRequest(Guid.NewGuid(), AdministrativeClockOutReason.Trim());
        IsMutating = true;
        try
        {
            await _api.AdministrativeClockOutAsync(sessionId, request);
            _pendingAdministrativeClockOut = null;
            AdministrativeClockOutReason = "";
            _notifications.ShowSuccess("Shift closed", "The cashier work period was closed by an administrator.");
            StatusMessage = "Shift closed for cashier and work period details refreshed.";
            await ReloadAfterMutationAsync(sessionId, resetForms: true);
        }
        catch (Exception exception) when (IsApiFailure(exception))
        {
            if (exception is ApiClientException) _pendingAdministrativeClockOut = null;
            else AdministrativeClockOutReason = request.Reason;
            ShowError("Shift could not be closed for the cashier", FailureMessage(exception));
        }
        finally
        {
            IsMutating = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanReviewAdjustment))]
    public Task ApproveAdjustmentAsync(CashierCashAdjustmentResponse? adjustment) =>
        ReviewAdjustmentAsync(adjustment, approve: true);

    [RelayCommand(CanExecute = nameof(CanReviewAdjustment))]
    public Task RejectAdjustmentAsync(CashierCashAdjustmentResponse? adjustment) =>
        ReviewAdjustmentAsync(adjustment, approve: false);

    [RelayCommand(CanExecute = nameof(CanCorrectAdjustment))]
    public async Task CorrectAdjustmentAsync(CashierCashAdjustmentResponse? adjustment)
    {
        if (!CanCorrectAdjustment(adjustment) || adjustment is null || Detail is null) return;
        var sessionId = Detail.Session.Id;
        IsMutating = true;
        try
        {
            await _api.CorrectCashAdjustmentAsync(adjustment.Id, new CorrectCashAdjustmentRequest(
                CorrectAdjustmentToApproved, AdjustmentCorrectionReason.Trim()));
            AdjustmentCorrectionReason = "";
            _notifications.ShowSuccess("Adjustment corrected", "The approved adjustment decision was updated.");
            await ReloadAfterMutationAsync(sessionId, resetForms: false);
        }
        catch (Exception exception) when (IsApiFailure(exception))
        {
            ShowError("Adjustment could not be updated", FailureMessage(exception));
        }
        finally
        {
            IsMutating = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanCorrectRemittance))]
    public async Task CorrectRemittanceAsync()
    {
        if (Detail is null || CorrectionValidationMessage is not null || !CorrectedActualRemittance.HasValue) return;
        var sessionId = Detail.Session.Id;
        IsMutating = true;
        try
        {
            await _api.CorrectShiftRemittanceAsync(sessionId, new CorrectShiftRemittanceRequest(
                CorrectedActualRemittance.Value,
                CorrectedCashFloatReturned,
                CorrectionReason.Trim()));
            CorrectionReason = "";
            _notifications.ShowSuccess("Remittance corrected", "The approved cash correction was recorded.");
            StatusMessage = "Approved cash remittance correction recorded and history refreshed.";
            await ReloadAfterMutationAsync(sessionId, resetForms: true);
        }
        catch (Exception exception) when (IsApiFailure(exception))
        {
            ShowError("Remittance correction could not be saved", FailureMessage(exception));
        }
        finally
        {
            IsMutating = false;
        }
    }

    partial void OnSelectedSessionChanged(CashierShiftSessionResponse? value)
    {
        if (_suppressSelectionLoad) return;
        _detailCancellation?.Cancel();
        Detail = null;
        DetailError = null;
        if (value is null)
        {
            ClearForms();
            return;
        }
        _ = LoadSessionDetailAsync(value.Id, resetForms: true);
    }

    partial void OnDetailChanged(CashierShiftSessionDetailResponse? value) => NotifyDetailState();
    partial void OnSessionsChanged(IReadOnlyList<CashierShiftSessionResponse> value)
    {
        OnPropertyChanged(nameof(HasSessions));
        NotifyPaging();
    }
    partial void OnReportChanged(CashierShiftReportResponse? value)
    {
        OnPropertyChanged(nameof(HasReport));
        OnPropertyChanged(nameof(ReportSessionsDisplay));
        OnPropertyChanged(nameof(ReportTotalSalesDisplay));
        OnPropertyChanged(nameof(ReportCashSalesDisplay));
        OnPropertyChanged(nameof(ReportGCashSalesDisplay));
        OnPropertyChanged(nameof(ReportExpectedDisplay));
        OnPropertyChanged(nameof(ReportActualDisplay));
        OnPropertyChanged(nameof(ReportVarianceDisplay));
    }
    partial void OnPageChanged(int value) => NotifyPaging();
    partial void OnTotalCountChanged(int value) => NotifyPaging();
    partial void OnPageSizeChanged(int oldValue, int newValue)
    {
        if (!PageSizeOptions.Contains(newValue) || IsLoading) return;
        Page = 1;
        _ = LoadPageAndReportAsync();
    }
    partial void OnIsLoadingChanged(bool value) => NotifyPaging();
    partial void OnIsDetailLoadingChanged(bool value) => OnPropertyChanged(nameof(ShowDetailPlaceholder));
    partial void OnIsMutatingChanged(bool value) => NotifyMutationCommands();
    partial void OnActualRemittanceChanged(decimal? value) => NotifyRemittanceState();
    partial void OnCashFloatReturnedChanged(bool value) => NotifyRemittanceState();
    partial void OnRemittanceNoteChanged(string value) => NotifyRemittanceState();
    partial void OnAdministrativeClockOutReasonChanged(string value)
    {
        OnPropertyChanged(nameof(AdministrativeClockOutValidationMessage));
        OnPropertyChanged(nameof(CanAdministrativeClockOutAction));
        AdministrativeClockOutCommand.NotifyCanExecuteChanged();
    }
    partial void OnCorrectedActualRemittanceChanged(decimal? value) => NotifyCorrectionState();
    partial void OnCorrectedCashFloatReturnedChanged(bool value) => NotifyCorrectionState();
    partial void OnCorrectionReasonChanged(string value) => NotifyCorrectionState();
    partial void OnAdjustmentReviewNoteChanged(string value)
    {
        ApproveAdjustmentCommand.NotifyCanExecuteChanged();
        RejectAdjustmentCommand.NotifyCanExecuteChanged();
    }
    partial void OnAdjustmentCorrectionReasonChanged(string value) => CorrectAdjustmentCommand.NotifyCanExecuteChanged();
    partial void OnCorrectAdjustmentToApprovedChanged(bool value) => CorrectAdjustmentCommand.NotifyCanExecuteChanged();

    private async Task LoadPageAndReportAsync()
    {
        _loadCancellation?.Cancel();
        _loadCancellation?.Dispose();
        _loadCancellation = new CancellationTokenSource();
        var cancellationToken = _loadCancellation.Token;
        var selectedId = SelectedSession?.Id;
        IsLoading = true;
        ListError = null;
        StatusMessage = "Loading cashier shift history and report summary...";
        try
        {
            var sessionsTask = _api.GetCashierShiftSessionsAsync(
                _appliedFilters.FromDate,
                _appliedFilters.ToDateExclusive,
                _appliedFilters.CashierUserId,
                _appliedFilters.ShiftDefinitionId,
                status: _appliedFilters.Status,
                page: Page,
                pageSize: PageSize,
                cancellationToken: cancellationToken);
            var reportTask = _api.GetCashierShiftReportAsync(
                _appliedFilters.FromDate,
                _appliedFilters.ToDateExclusive,
                _appliedFilters.CashierUserId,
                _appliedFilters.ShiftDefinitionId,
                cancellationToken: cancellationToken);
            var usersTask = _api.GetUsersAsync(includeInactive: true, cancellationToken);
            var definitionsTask = _api.GetShiftDefinitionsAsync(includeInactive: true, cancellationToken);
            var salesTask = _api.GetSalesReportAsync(
                StoreDateTime.AtStoreMidnight(_appliedFilters.FromDate.ToDateTime(TimeOnly.MinValue)).ToUniversalTime(),
                StoreDateTime.AtStoreMidnight(_appliedFilters.ToDateExclusive.ToDateTime(TimeOnly.MinValue)).ToUniversalTime(),
                cancellationToken: cancellationToken);
            await Task.WhenAll(sessionsTask, reportTask, usersTask, definitionsTask, salesTask);

            var result = sessionsTask.Result;
            Sessions = result.Items;
            Page = result.Page;
            TotalCount = result.TotalCount;
            Report = reportTask.Result;
            var selectedCashierId = SelectedCashier?.Id;
            Cashiers = usersTask.Result
                .Where(user => user.Roles.Contains("Cashier", StringComparer.OrdinalIgnoreCase))
                .OrderBy(user => user.DisplayName)
                .ToArray();
            SelectedCashier = Cashiers.FirstOrDefault(user => user.Id == selectedCashierId);
            var selectedShiftId = SelectedShiftDefinition?.Id;
            ShiftDefinitions = definitionsTask.Result.OrderBy(item => item.StartLocalTime).ToArray();
            SelectedShiftDefinition = ShiftDefinitions.FirstOrDefault(item => item.Id == selectedShiftId);
            _transactionReport = salesTask.Result;
            ApplyTransactionFilter();

            var selected = selectedId.HasValue
                ? Sessions.FirstOrDefault(item => item.Id == selectedId.Value) ??
                  (Detail?.Session.Id == selectedId.Value ? Detail.Session : null)
                : null;
            _suppressSelectionLoad = true;
            SelectedSession = selected;
            _suppressSelectionLoad = false;
            if (selected is null)
            {
                Detail = null;
                DetailError = null;
                ClearForms();
            }
            else
            {
                await LoadSessionDetailAsync(selected.Id, resetForms: false);
            }
            StatusMessage = $"Loaded {Sessions.Count} work period{(Sessions.Count == 1 ? "" : "s")} on this page. Report totals are calculated from the system records.";
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception) when (IsApiFailure(exception))
        {
            ListError = FailureMessage(exception);
            StatusMessage = ListError;
            _notifications.ShowError("Cashier operations could not be loaded", ListError);
        }
        catch (Exception exception)
        {
            ListError = $"Cashier operations could not be loaded: {exception.Message}";
            StatusMessage = ListError;
            _notifications.ShowError("Cashier operations could not be loaded", ListError);
        }
        finally
        {
            _suppressSelectionLoad = false;
            IsLoading = false;
        }
    }

    private async Task LoadSessionDetailAsync(Guid sessionId, bool resetForms)
    {
        _detailCancellation?.Cancel();
        _detailCancellation?.Dispose();
        _detailCancellation = new CancellationTokenSource();
        var cancellationToken = _detailCancellation.Token;
        IsDetailLoading = true;
        DetailError = null;
        try
        {
            var detail = await _api.GetCashierShiftSessionAsync(sessionId, cancellationToken);
            if (SelectedSession?.Id != sessionId) return;
            Detail = detail;
            if (resetForms) InitializeForms(detail.Session);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception) when (IsApiFailure(exception))
        {
            if (SelectedSession?.Id == sessionId)
            {
                DetailError = FailureMessage(exception);
                _notifications.ShowError("Work period details could not be loaded", DetailError);
            }
        }
        catch (Exception exception)
        {
            if (SelectedSession?.Id == sessionId)
            {
                DetailError = $"Work period details could not be loaded: {exception.Message}";
                _notifications.ShowError("Work period details could not be loaded", DetailError);
            }
        }
        finally
        {
            if (SelectedSession?.Id == sessionId) IsDetailLoading = false;
        }
    }

    private void ApplyTransactionFilter()
    {
        if (_transactionReport is null) return;
        var from = TransactionFromDate?.Date;
        var to = TransactionToDate?.Date;
        var sales = _transactionReport.Sales.Where(sale =>
            (!from.HasValue || sale.SoldAtUtc.ToLocalTime().Date >= from.Value) &&
            (!to.HasValue || sale.SoldAtUtc.ToLocalTime().Date <= to.Value) &&
            (TransactionCashier is null || sale.SoldByUserId == TransactionCashier.Id));
        if (TransactionShift is not null)
            sales = sales.Where(sale => sale.ShiftDefinitionId == TransactionShift.Id);
        TransactionSales = sales.OrderByDescending(sale => sale.SoldAtUtc).ToArray();
        TransactionStatusMessage = $"Showing {TransactionSales.Count:N0} transaction{(TransactionSales.Count == 1 ? "" : "s")}. Select a sale for details.";
    }

    partial void OnSelectedTransactionDateRangeChanged(TransactionDateRangeOption value)
    {
        var today = StoreDateTime.StoreToday;
        (TransactionFromDate, TransactionToDate) = value.Label switch
        {
            "Yesterday" => (StoreDateTime.AtStoreMidnight(today.AddDays(-1)), StoreDateTime.AtStoreMidnight(today)),
            "This week" => (StoreDateTime.AtStoreMidnight(today.AddDays(-((int)today.DayOfWeek + 6) % 7)), StoreDateTime.AtStoreMidnight(today.AddDays(1))),
            "This month" => (StoreDateTime.AtStoreMidnight(new DateTime(today.Year, today.Month, 1)), StoreDateTime.AtStoreMidnight(today.AddDays(1))),
            "All time" => (null, null),
            "Custom" => (TransactionFromDate, TransactionToDate),
            _ => (StoreDateTime.AtStoreMidnight(today), StoreDateTime.AtStoreMidnight(today.AddDays(1)))
        };
        OnPropertyChanged(nameof(IsTransactionCustomDateRange));
        if (value.Label != "Custom") ApplyTransactionFilter();
    }

    partial void OnSelectedHistoryDateRangeChanged(TransactionDateRangeOption value)
    {
        var today = StoreDateTime.StoreToday;
        (FromDate, ToDate) = value.Label switch
        {
            "Yesterday" => (StoreDateTime.AtStoreMidnight(today.AddDays(-1)), StoreDateTime.AtStoreMidnight(today.AddDays(-1))),
            "This week" => (StoreDateTime.AtStoreMidnight(today.AddDays(-((int)today.DayOfWeek + 6) % 7)), StoreDateTime.AtStoreMidnight(today)),
            "This month" => (StoreDateTime.AtStoreMidnight(new DateTime(today.Year, today.Month, 1)), StoreDateTime.AtStoreMidnight(today)),
            "All time" => (StoreDateTime.AtStoreMidnight(today.AddDays(-365)), StoreDateTime.AtStoreMidnight(today)),
            "Custom" => (FromDate, ToDate),
            _ => (StoreDateTime.AtStoreMidnight(today), StoreDateTime.AtStoreMidnight(today))
        };
        OnPropertyChanged(nameof(IsHistoryCustomDateRange));
    }

    private async Task ReviewAdjustmentAsync(CashierCashAdjustmentResponse? adjustment, bool approve)
    {
        if (!CanReviewAdjustment(adjustment) || adjustment is null || Detail is null) return;
        var sessionId = Detail.Session.Id;
        IsMutating = true;
        try
        {
            var request = new CashAdjustmentReviewRequest(NullIfWhiteSpace(AdjustmentReviewNote));
            if (approve) await _api.ApproveCashAdjustmentAsync(adjustment.Id, request);
            else await _api.RejectCashAdjustmentAsync(adjustment.Id, request);
            AdjustmentReviewNote = "";
            var action = approve ? "approved" : "rejected";
            _notifications.ShowSuccess($"Adjustment {action}", $"The {adjustment.Type} request was {action}.");
            StatusMessage = $"Cash adjustment {action} and work period details refreshed.";
            await ReloadAfterMutationAsync(sessionId, resetForms: false);
        }
        catch (Exception exception) when (IsApiFailure(exception))
        {
            ShowError(approve ? "Adjustment could not be approved" : "Adjustment could not be rejected", FailureMessage(exception));
        }
        finally
        {
            IsMutating = false;
        }
    }

    private async Task ReloadAfterMutationAsync(Guid sessionId, bool resetForms)
    {
        await LoadPageAndReportAsync();
        if (SelectedSession?.Id == sessionId)
            await LoadSessionDetailAsync(sessionId, resetForms);
    }

    private void InitializeForms(CashierShiftSessionResponse session)
    {
        ActualRemittance = session.ActualRemittance;
        CashFloatReturned = session.CashFloatReturned ?? false;
        RemittanceNote = session.RemittanceNote ?? "";
        AdministrativeClockOutReason = "";
        CorrectedActualRemittance = session.ActualRemittance;
        CorrectedCashFloatReturned = session.CashFloatReturned ?? false;
        CorrectionReason = "";
        AdjustmentReviewNote = "";
        AdjustmentCorrectionReason = "";
    }

    private void ClearForms()
    {
        ActualRemittance = null;
        CashFloatReturned = false;
        RemittanceNote = "";
        AdministrativeClockOutReason = "";
        CorrectedActualRemittance = null;
        CorrectedCashFloatReturned = false;
        CorrectionReason = "";
        AdjustmentReviewNote = "";
        AdjustmentCorrectionReason = "";
    }

    private void NotifyDetailState()
    {
        OnPropertyChanged(nameof(HasDetail));
        OnPropertyChanged(nameof(ShowDetailPlaceholder));
        OnPropertyChanged(nameof(HasSales));
        OnPropertyChanged(nameof(HasAdjustments));
        OnPropertyChanged(nameof(HasCorrections));
        OnPropertyChanged(nameof(CanShowAdministrativeClockOut));
        OnPropertyChanged(nameof(HasBlockingPendingAdjustments));
        OnPropertyChanged(nameof(CanShowRemittanceForm));
        OnPropertyChanged(nameof(CanShowCorrectionForm));
        OnPropertyChanged(nameof(AdministrativeClockOutValidationMessage));
        OnPropertyChanged(nameof(CanAdministrativeClockOutAction));
        NotifyRemittanceState();
        NotifyCorrectionState();
        NotifyMutationCommands();
    }

    private void NotifyRemittanceState()
    {
        OnPropertyChanged(nameof(ProvisionalVariance));
        OnPropertyChanged(nameof(ProvisionalVarianceDisplay));
        OnPropertyChanged(nameof(ProvisionalVarianceState));
        OnPropertyChanged(nameof(RemittanceValidationMessage));
        RecordRemittanceCommand.NotifyCanExecuteChanged();
    }

    private void NotifyCorrectionState()
    {
        OnPropertyChanged(nameof(CorrectionValidationMessage));
        CorrectRemittanceCommand.NotifyCanExecuteChanged();
    }

    private void NotifyMutationCommands()
    {
        RecordRemittanceCommand.NotifyCanExecuteChanged();
        AdministrativeClockOutCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(CanAdministrativeClockOutAction));
        CorrectRemittanceCommand.NotifyCanExecuteChanged();
        ApproveAdjustmentCommand.NotifyCanExecuteChanged();
        RejectAdjustmentCommand.NotifyCanExecuteChanged();
        CorrectAdjustmentCommand.NotifyCanExecuteChanged();
    }

    private void NotifyPaging()
    {
        OnPropertyChanged(nameof(PageSummary));
        PreviousPageCommand.NotifyCanExecuteChanged();
        NextPageCommand.NotifyCanExecuteChanged();
    }

    private void ShowError(string title, string message)
    {
        StatusMessage = message;
        _notifications.ShowError(title, message);
    }

    private static string? NullIfWhiteSpace(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static DateOnly ToDateOnly(DateTimeOffset value) => DateOnly.FromDateTime(value.Date);
    private static bool IsApiFailure(Exception exception) => exception is ApiClientException or HttpRequestException or TaskCanceledException;
    private static string FailureMessage(Exception exception) => exception switch
    {
        HttpRequestException => "Cannot reach the store API.",
        TaskCanceledException => "The store API did not respond in time.",
        _ => exception.Message
    };

    private sealed record AppliedFilters(
        DateOnly FromDate, DateOnly ToDateExclusive, Guid? CashierUserId,
        Guid? ShiftDefinitionId, ApiCashierShiftSessionStatus? Status);

    public void Dispose()
    {
        _loadCancellation?.Cancel();
        _loadCancellation?.Dispose();
        _detailCancellation?.Cancel();
        _detailCancellation?.Dispose();
    }
}
