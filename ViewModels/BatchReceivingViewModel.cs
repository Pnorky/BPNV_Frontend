using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using AvaloniaApp.Services;
using AvaloniaApp.Views.Dialogs;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AvaloniaApp.ViewModels;

public sealed record BatchReceivingDisplayIssue(string Severity, string Location, string Field, string Code, string Message);

public partial class BatchReceivingRowViewModel : ObservableObject
{
    private readonly Action<BatchReceivingRowViewModel> _priceChanged;
    private bool _suppressChanges;

    [ObservableProperty] private decimal? _costPrice;
    [ObservableProperty] private decimal? _regularPrice;
    [ObservableProperty] private decimal? _employeePrice;
    [ObservableProperty] private bool _isExpanded;

    public IReadOnlyList<int> SourceRecords { get; }
    public string SupplierLibrary { get; }
    public string Barcode { get; }
    public Guid? SupplierId { get; }
    public string? SupplierName { get; }
    public Guid? ProductId { get; }
    public string ProductNameDisplay { get; }
    public string? Sku { get; }
    public Guid? UnitId { get; }
    public string? UnitLabel { get; }
    public int InputUnitQuantity { get; }
    public int? PiecesPerUnit { get; }
    public int? BasePieceQuantity { get; }
    public int? CurrentBodegaBalance { get; }
    public int? ProjectedBodegaBalance { get; }
    public string Status { get; }
    public IReadOnlyList<BatchReceiptIssueResponse> Issues { get; }
    public bool IsNewProduct { get; }
    public Guid? NewProductCorrelationId { get; }
    public decimal? PreviousCostPrice { get; }
    public decimal? PreviousRegularPrice { get; }
    public decimal? PreviousEmployeePrice { get; }
    public ulong? ProductVersion { get; }
    public bool CanEditPrices => ProductId.HasValue || IsNewProduct;
    public bool CanManageProduct => ProductId is null;
    public string ProductActionLabel => IsNewProduct ? "Edit product" : "Add product";
    public string SupplierResolutionDisplay => $"{SupplierLibrary} -> {SupplierName ?? "Unknown"}";
    public string ScannedQuantityDisplay => PiecesPerUnit is > 1
        ? $"{InputUnitQuantity:N0} {UnitLabel ?? "unit"} x {PiecesPerUnit:N0}"
        : string.IsNullOrWhiteSpace(UnitLabel) ? InputUnitQuantity.ToString("N0") : $"{InputUnitQuantity:N0} {UnitLabel}";
    public string BasePieceQuantityDisplay => BasePieceQuantity?.ToString("N0") ?? "-";
    public string BodegaChangeDisplay => $"{CurrentBodegaBalance?.ToString("N0") ?? "-"} -> {ProjectedBodegaBalance?.ToString("N0") ?? "-"}";
    public string PreviousCostDisplay => PreviousPriceDisplay(PreviousCostPrice);
    public string PreviousRegularDisplay => PreviousPriceDisplay(PreviousRegularPrice);
    public string PreviousEmployeeDisplay => PreviousPriceDisplay(PreviousEmployeePrice);
    public decimal? TotalCost => BasePieceQuantity.HasValue && CostPrice.HasValue ? BasePieceQuantity.Value * CostPrice.Value : null;
    public string TotalCostDisplay => TotalCost.HasValue ? $"₱{TotalCost:N2}" : "-";
    public string DetailsActionLabel => IsExpanded ? "Hide details" : "View details";

    public BatchReceivingRowViewModel(BatchReceiptPreviewRowResponse row, Action<BatchReceivingRowViewModel> priceChanged)
    {
        _priceChanged = priceChanged;
        SourceRecords = row.SourceRecords;
        SupplierLibrary = row.SupplierLibrary;
        Barcode = row.Barcode;
        SupplierId = row.SupplierId;
        SupplierName = row.SupplierName;
        ProductId = row.ProductId;
        ProductNameDisplay = row.ProductNameDisplay;
        Sku = row.Sku;
        UnitId = row.UnitId;
        UnitLabel = row.UnitLabel;
        InputUnitQuantity = row.InputUnitQuantity;
        PiecesPerUnit = row.PiecesPerUnit;
        BasePieceQuantity = row.BasePieceQuantity;
        CurrentBodegaBalance = row.CurrentBodegaBalance;
        ProjectedBodegaBalance = row.ProjectedBodegaBalance;
        Status = row.Status;
        Issues = row.Issues;
        IsNewProduct = row.IsNewProduct;
        NewProductCorrelationId = row.NewProductCorrelationId;
        PreviousCostPrice = row.PreviousCostPrice;
        PreviousRegularPrice = row.PreviousRegularPrice;
        PreviousEmployeePrice = row.PreviousEmployeePrice;
        ProductVersion = row.ProductVersion;
        _suppressChanges = true;
        CostPrice = row.CostPrice;
        RegularPrice = row.RegularPrice;
        EmployeePrice = row.EmployeePrice;
        _suppressChanges = false;
    }

    partial void OnCostPriceChanged(decimal? value) => NotifyPriceChanged();
    partial void OnRegularPriceChanged(decimal? value) => NotifyPriceChanged();
    partial void OnEmployeePriceChanged(decimal? value) => NotifyPriceChanged();
    partial void OnIsExpandedChanged(bool value) => OnPropertyChanged(nameof(DetailsActionLabel));

    public void SetPrices(decimal? costPrice, decimal? regularPrice, decimal? employeePrice)
    {
        _suppressChanges = true;
        CostPrice = costPrice;
        RegularPrice = regularPrice;
        EmployeePrice = employeePrice;
        _suppressChanges = false;
        OnPropertyChanged(nameof(TotalCost));
        OnPropertyChanged(nameof(TotalCostDisplay));
    }

    private void NotifyPriceChanged()
    {
        OnPropertyChanged(nameof(TotalCost));
        OnPropertyChanged(nameof(TotalCostDisplay));
        if (!_suppressChanges) _priceChanged(this);
    }

    private string PreviousPriceDisplay(decimal? price) => IsNewProduct
        ? "New product"
        : price.HasValue ? $"Previous ₱{price:N2}" : "Not available";
}

public partial class BatchReceivingViewModel : ObservableObject
{
    private readonly StoreApiClient _api;
    private readonly INotificationService _notifications;
    private readonly EyoyoBatchReceiveParser _parser;
    private BatchReceiptRequest? _validatedRequest;
    private string? _validatedFingerprint;
    private bool _suppressDraftChanges;
    private bool _suppressRowChanges;
    private bool _commitAttempted;
    private readonly Dictionary<Guid, BatchReceiptPriceUpdateRequest> _priceUpdates = [];
    private readonly Dictionary<string, BatchReceiptNewProductRequest> _newProducts = new(StringComparer.Ordinal);
    private BatchReceivingRowViewModel? _expandedPreviewRow;

    [ObservableProperty] private string _captureText = "";
    [ObservableProperty] private string _reference = "";
    [ObservableProperty] private string _notes = "";
    [ObservableProperty] private bool _useCommitTime = true;
    [ObservableProperty] private DateTimeOffset? _deliveryDate;
    [ObservableProperty] private TimeSpan? _deliveryTime;
    [ObservableProperty] private string _statusMessage = "Focus the capture field, run Eyoyo Keyboard Export, then review the batch.";
    [ObservableProperty] private string _validationSummary = "Not reviewed";
    [ObservableProperty] private string? _previewError;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _backendCanCommit;
    [ObservableProperty] private BatchReceiptResponse? _result;
    [ObservableProperty] private IReadOnlyList<BatchReceivingRowViewModel> _previewRows = [];

    public ObservableCollection<BatchReceivingDisplayIssue> Issues { get; } = [];
    public TablePager<BatchReceivingRowViewModel> PreviewPager { get; } = new([], "preview line", "preview lines");
    public Guid IdempotencyKey { get; private set; } = Guid.NewGuid();
    public bool CanReview => !IsBusy && !_commitAttempted && !string.IsNullOrWhiteSpace(CaptureText);
    public bool CanCommit => !IsBusy && BackendCanCommit && _validatedRequest is not null;
    public bool CanEdit => !IsBusy;
    public bool HasPreview => PreviewRows.Count > 0;
    public bool HasIssues => Issues.Count > 0;
    public bool HasResult => Result is not null;
    public bool IsManualDeliveryTime => !UseCommitTime;
    public DateTimeOffset? DeliveryAtUtc => UseCommitTime || !DeliveryDate.HasValue || !DeliveryTime.HasValue
        ? null
        : StoreDateTime.CombineStoreDateAndTimeToUtc(DeliveryDate.Value, DeliveryTime.Value);
    public string DeliveryTimePreview => UseCommitTime
        ? "Exact commit time"
        : DeliveryAtUtc.HasValue
            ? $"{StoreDateTime.FormatUtc(DeliveryAtUtc.Value.UtcDateTime)} (Philippine time)"
            : "Select a Philippine delivery date and time";
    public string DeliveryTimeConfirmationText => UseCommitTime
        ? "Delivery time: exact server commit time"
        : $"Delivery time: {DeliveryTimePreview}";
    public string DeliveryTimeHelpText => UseCommitTime
        ? "Set automatically by the server when this delivery is received."
        : "The selected Philippine time is converted to UTC before it is sent.";

    public BatchReceivingViewModel(
        StoreApiClient api,
        INotificationService notifications,
        EyoyoBatchReceiveParser? parser = null)
    {
        _api = api;
        _notifications = notifications;
        _parser = parser ?? new EyoyoBatchReceiveParser();
    }

    partial void OnCaptureTextChanged(string value)
    {
        if (!_suppressDraftChanges)
        {
            _priceUpdates.Clear();
            _newProducts.Clear();
        }
        DraftChanged();
    }
    partial void OnReferenceChanged(string value) => DraftChanged();
    partial void OnNotesChanged(string value) => DraftChanged();
    partial void OnUseCommitTimeChanged(bool value)
    {
        if (!value && (!DeliveryDate.HasValue || !DeliveryTime.HasValue))
        {
            var now = StoreDateTime.StoreNow;
            _suppressDraftChanges = true;
            DeliveryDate ??= StoreDateTime.AtStoreMidnight(now);
            DeliveryTime ??= now.TimeOfDay;
            _suppressDraftChanges = false;
        }
        NotifyDeliveryTimeChanged();
        DraftChanged();
    }
    partial void OnDeliveryDateChanged(DateTimeOffset? value)
    {
        NotifyDeliveryTimeChanged();
        DraftChanged();
    }
    partial void OnDeliveryTimeChanged(TimeSpan? value)
    {
        NotifyDeliveryTimeChanged();
        DraftChanged();
    }
    partial void OnIsBusyChanged(bool value)
    {
        PreviewPager.SetLoading(value);
        OnPropertyChanged(nameof(CanEdit));
        NotifyActions();
    }
    partial void OnBackendCanCommitChanged(bool value) => OnPropertyChanged(nameof(CanCommit));
    partial void OnResultChanged(BatchReceiptResponse? value) => OnPropertyChanged(nameof(HasResult));
    partial void OnPreviewErrorChanged(string? value)
    {
        PreviewPager.SetError(value);
    }
    partial void OnPreviewRowsChanged(IReadOnlyList<BatchReceivingRowViewModel> value)
    {
        _expandedPreviewRow = null;
        PreviewPager.SetItems(value);
        OnPropertyChanged(nameof(HasPreview));
    }

    [RelayCommand]
    public async Task ReviewBatchAsync()
    {
        if (!CanReview)
        {
            if (_commitAttempted)
                StatusMessage = "The previous receipt result is uncertain. Retry the exact batch with Receive, or edit or clear the draft to start with a new key.";
            return;
        }
        InvalidatePreview(clearResult: true, clearRows: false);

        var parsed = _parser.Parse(CaptureText);
        foreach (var issue in parsed.Issues) AddIssue("Error", issue.SourceRecord, issue.Field, issue.Code, issue.Message);
        RefreshStateProperties();
        if (!parsed.IsValid)
        {
            ValidationSummary = $"Local parsing found {Issues.Count} blocking issue{Plural(Issues.Count)}.";
            StatusMessage = "Correct the scanner capture, then review the batch again. The raw capture has been preserved.";
            _notifications.ShowError("Batch capture needs attention", StatusMessage);
            return;
        }

        if (!TryBuildRequest(parsed.Records, out var request, out var requestError))
        {
            StatusMessage = requestError;
            _notifications.ShowError("Batch draft needs attention", requestError);
            return;
        }
        PreviewRows = [];
        var fingerprint = Fingerprint(request);
        IsBusy = true;
        StatusMessage = "Validating barcodes, suppliers, package conversions, and Bodega balances...";
        try
        {
            var response = await _api.ValidateBatchReceiptAsync(request);
            var currentParse = _parser.Parse(CaptureText);
            if (!currentParse.IsValid || fingerprint != Fingerprint(BuildRequest(currentParse.Records)))
            {
                InvalidatePreview(clearResult: true);
                StatusMessage = "The draft changed during validation. Review it again.";
                return;
            }
            if (response.IdempotencyKey != request.IdempotencyKey)
                throw new InvalidOperationException("The API returned a validation result for a different draft.");

            _suppressRowChanges = true;
            PreviewRows = response.Rows.Select(row => new BatchReceivingRowViewModel(row, OnRowPriceChanged)).ToArray();
            foreach (var row in PreviewRows.Where(row => row.ProductId.HasValue && row.ProductVersion.HasValue))
            {
                if (_priceUpdates.TryGetValue(row.ProductId!.Value, out var update))
                    _priceUpdates[row.ProductId.Value] = update with { ExpectedProductVersion = row.ProductVersion.GetValueOrDefault() };
            }
            _suppressRowChanges = false;
            foreach (var issue in response.Issues) AddIssue(issue.Severity, issue.SourceRecord, issue.Field, issue.Code, issue.Message);
            var summary = response.Summary;
            ValidationSummary = $"{summary.InputRecordCount:N0} input records, {summary.NormalizedLineCount:N0} preview lines, " +
                $"{summary.AffectedProductCount:N0} products, {summary.TotalBasePieces?.ToString("N0") ?? "-"} base pieces, " +
                $"{(summary.TotalCost.HasValue ? $"₱{summary.TotalCost:N2}" : "-")} total cost, " +
                $"{summary.WarningCount:N0} warnings, {summary.ErrorCount:N0} errors, {summary.IssueCount:N0} findings";
            BackendCanCommit = response.CanCommit;
            _validatedRequest = response.CanCommit ? request : null;
            _validatedFingerprint = response.CanCommit ? fingerprint : null;
            if (!response.CanCommit)
            {
                StatusMessage = $"Validation found {summary.ErrorCount:N0} blocking error{Plural(summary.ErrorCount)}" +
                    (summary.WarningCount > 0 ? $" and {summary.WarningCount:N0} warning{Plural(summary.WarningCount)}" : "") +
                    ". No stock has been changed.";
                _notifications.ShowError("Batch validation blocked", StatusMessage);
            }
            else if (summary.WarningCount > 0)
            {
                StatusMessage = $"Validation passed with {summary.WarningCount:N0} warning{Plural(summary.WarningCount)}. " +
                    "Review the findings; the registered supplier resolved from each barcode is authoritative and will be used for receipt.";
                _notifications.ShowWarning("Batch validation passed with warnings", StatusMessage);
            }
            else
            {
                StatusMessage = "Validation passed with no findings. Review every line, then receive the batch into Bodega.";
                _notifications.ShowSuccess("Batch validation passed", StatusMessage);
            }
            RefreshStateProperties();
        }
        catch (Exception exception) when (IsApiFailure(exception))
        {
            PreviewError = FailureMessage(exception);
            StatusMessage = $"Validation failed: {PreviewError} The raw capture has been preserved.";
            _notifications.ShowError("Batch validation failed", StatusMessage);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task CommitBatchAsync()
    {
        if (!TryGetValidatedRequest(out _)) return;

        var window = MainWindow();
        if (window is null) return;
        var dialog = new ConfirmDialog();
        dialog.SetConfirmation(
            "Receive batch into Bodega?",
            $"This will atomically receive {PreviewRows.Count:N0} validated line{Plural(PreviewRows.Count)} into Bodega. " +
            $"No stock will be added to Display.{Environment.NewLine}{DeliveryTimeConfirmationText}.",
            "Receive into Bodega");
        await dialog.ShowDialog(window);
        if (!dialog.Confirmed) return;

        await SubmitValidatedBatchAsync();
    }

    public async Task SubmitValidatedBatchAsync()
    {
        if (!TryGetValidatedRequest(out var request)) return;

        SetCommitAttempted(true);
        IsBusy = true;
        StatusMessage = "Receiving the validated batch into Bodega...";
        try
        {
            var response = await _api.ReceiveBatchAsync(request!);
            ResetAfterSuccess(response);
            StatusMessage = response.IsIdempotentReplay
                ? "This batch was already received. The original successful result is shown below."
                : "Batch received successfully into Bodega.";
            _notifications.ShowSuccess("Batch received", StatusMessage);
            PublishPriceChangeNotifications(response.PriceChanges ?? []);
        }
        catch (ApiClientException exception)
        {
            SetCommitAttempted(false);
            BackendCanCommit = false;
            _validatedRequest = null;
            _validatedFingerprint = null;
            AddIssue("Error", null, "batch", "commitFailed", exception.Message);
            StatusMessage = $"Receipt failed: {exception.Message} Review the batch again before retrying. The capture and draft key were preserved.";
            _notifications.ShowError("Batch receipt failed", StatusMessage);
            RefreshStateProperties();
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            StatusMessage = $"Receipt failed: {FailureMessage(exception)} Retry uses the same draft key so stock cannot be received twice.";
            _notifications.ShowError("Batch receipt failed", StatusMessage);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void TogglePreviewRow(BatchReceivingRowViewModel? row)
    {
        if (row is null) return;
        if (ReferenceEquals(_expandedPreviewRow, row))
        {
            row.IsExpanded = false;
            _expandedPreviewRow = null;
            return;
        }

        if (_expandedPreviewRow is not null) _expandedPreviewRow.IsExpanded = false;
        row.IsExpanded = true;
        _expandedPreviewRow = row;
    }

    [RelayCommand]
    private async Task AddUnknownProductAsync(BatchReceivingRowViewModel? row)
    {
        if (row is null || !row.CanManageProduct || IsBusy) return;
        IsBusy = true;
        StatusMessage = "Loading active suppliers for the new product...";
        IReadOnlyList<SupplierResponse> suppliers;
        try
        {
            suppliers = await _api.GetSuppliersAsync();
        }
        catch (Exception exception) when (IsApiFailure(exception))
        {
            ShowError("Could not add product", FailureMessage(exception));
            return;
        }
        finally
        {
            IsBusy = false;
        }

        var owner = MainWindow();
        if (owner is null) return;
        _newProducts.TryGetValue(row.Barcode, out var existing);
        var correlationId = existing?.CorrelationId ?? row.NewProductCorrelationId ?? Guid.NewGuid();
        var model = new BatchNewProductViewModel(suppliers, row.Barcode, row.SupplierLibrary, existing);
        var draft = await new BatchNewProductDialog(model, correlationId).ShowDialog<BatchReceiptNewProductRequest?>(owner);
        if (draft is not null) await ApplyNewProductDraftAndReviewAsync(draft);
    }

    internal async Task ApplyNewProductDraftAndReviewAsync(BatchReceiptNewProductRequest draft)
    {
        _newProducts[draft.ReceiptBarcode] = draft;
        EditableDraftChanged("New product draft added. Revalidating the batch...");
        await ReviewBatchAsync();
    }

    [RelayCommand]
    public async Task ClearDraftAsync()
    {
        if (IsBusy) return;
        if (HasDraftData())
        {
            var window = MainWindow();
            if (window is null) return;
            var dialog = new ConfirmDialog();
            dialog.SetConfirmation("Clear batch draft?", "This removes the raw scanner capture, preview, receipt details, delivery time, and successful result.", "Clear draft");
            await dialog.ShowDialog(window);
            if (!dialog.Confirmed) return;
        }

        ResetDraft();
        StatusMessage = "Draft cleared. Focus the capture field to start a new batch.";
    }

    private BatchReceiptRequest BuildRequest(IReadOnlyList<BatchReceiptRecordRequest> records) => new(
        IdempotencyKey,
        NullIfWhiteSpace(Reference),
        NullIfWhiteSpace(Notes),
        records,
        _priceUpdates.Values.OrderBy(update => update.ProductId).ToArray(),
        _newProducts.Values.OrderBy(product => product.CorrelationId).ToArray(),
        DeliveryAtUtc);

    private bool TryBuildRequest(
        IReadOnlyList<BatchReceiptRecordRequest> records,
        out BatchReceiptRequest request,
        out string error)
    {
        if (!UseCommitTime && (!DeliveryDate.HasValue || !DeliveryTime.HasValue))
        {
            request = null!;
            error = "Select both the Philippine delivery date and time before reviewing the batch.";
            return false;
        }
        foreach (var row in PreviewRows.Where(row => row.CanEditPrices))
        {
            if (!row.CostPrice.HasValue || !row.RegularPrice.HasValue || !row.EmployeePrice.HasValue ||
                row.CostPrice < 0 || row.RegularPrice < 0 || row.EmployeePrice < 0)
            {
                request = null!;
                error = $"Enter valid non-negative prices for {row.ProductNameDisplay}.";
                return false;
            }
        }
        request = BuildRequest(records);
        error = "";
        return true;
    }

    private void DraftChanged()
    {
        if (_suppressDraftChanges) return;
        if (_commitAttempted)
        {
            IdempotencyKey = Guid.NewGuid();
            SetCommitAttempted(false);
            OnPropertyChanged(nameof(IdempotencyKey));
        }
        InvalidatePreview(clearResult: true, clearRows: true);
        StatusMessage = "Draft changed. Select Review Batch to validate the current capture.";
        NotifyActions();
    }

    private void EditableDraftChanged(string message = "Prices changed. Select Review Batch to validate the latest values.")
    {
        if (_suppressRowChanges) return;
        if (_commitAttempted)
        {
            IdempotencyKey = Guid.NewGuid();
            SetCommitAttempted(false);
            OnPropertyChanged(nameof(IdempotencyKey));
        }
        InvalidatePreview(clearResult: true, clearRows: false);
        StatusMessage = message;
    }

    private void InvalidatePreview(bool clearResult, bool clearRows = true)
    {
        BackendCanCommit = false;
        _validatedRequest = null;
        _validatedFingerprint = null;
        PreviewError = null;
        if (clearRows) PreviewRows = [];
        Issues.Clear();
        ValidationSummary = "Not reviewed";
        if (clearResult) Result = null;
        RefreshStateProperties();
    }

    private void ResetAfterSuccess(BatchReceiptResponse response)
    {
        _suppressDraftChanges = true;
        CaptureText = "";
        Reference = "";
        Notes = "";
        UseCommitTime = true;
        DeliveryDate = null;
        DeliveryTime = null;
        _suppressDraftChanges = false;
        PreviewRows = [];
        Issues.Clear();
        PreviewError = null;
        ValidationSummary = "Receipt completed";
        BackendCanCommit = false;
        _validatedRequest = null;
        _validatedFingerprint = null;
        _priceUpdates.Clear();
        _newProducts.Clear();
        SetCommitAttempted(false);
        IdempotencyKey = Guid.NewGuid();
        OnPropertyChanged(nameof(IdempotencyKey));
        Result = response;
        RefreshStateProperties();
    }

    private void ResetDraft()
    {
        _suppressDraftChanges = true;
        CaptureText = "";
        Reference = "";
        Notes = "";
        UseCommitTime = true;
        DeliveryDate = null;
        DeliveryTime = null;
        _suppressDraftChanges = false;
        _priceUpdates.Clear();
        _newProducts.Clear();
        SetCommitAttempted(false);
        IdempotencyKey = Guid.NewGuid();
        OnPropertyChanged(nameof(IdempotencyKey));
        InvalidatePreview(clearResult: true, clearRows: true);
        NotifyActions();
    }

    private bool HasDraftData() =>
        !string.IsNullOrEmpty(CaptureText) || !string.IsNullOrWhiteSpace(Reference) || !string.IsNullOrWhiteSpace(Notes) ||
        !UseCommitTime || PreviewRows.Count > 0 || Issues.Count > 0 || Result is not null || _priceUpdates.Count > 0 || _newProducts.Count > 0;

    private bool TryGetValidatedRequest(out BatchReceiptRequest? request)
    {
        request = _validatedRequest;
        if (IsBusy || request is null)
        {
            StatusMessage = "Review and pass backend validation before receiving this batch.";
            return false;
        }

        var currentParse = _parser.Parse(CaptureText);
        if (!currentParse.IsValid || _validatedFingerprint != Fingerprint(BuildRequest(currentParse.Records)))
        {
            request = null;
            InvalidatePreview(clearResult: true, clearRows: false);
            StatusMessage = "The draft changed after validation. Review it again before receiving stock.";
            return false;
        }

        return true;
    }

    private void OnRowPriceChanged(BatchReceivingRowViewModel changed)
    {
        if (_suppressRowChanges) return;
        _suppressRowChanges = true;
        foreach (var row in PreviewRows.Where(row => row != changed &&
                     (changed.ProductId.HasValue && row.ProductId == changed.ProductId ||
                      changed.IsNewProduct && row.NewProductCorrelationId == changed.NewProductCorrelationId)))
            row.SetPrices(changed.CostPrice, changed.RegularPrice, changed.EmployeePrice);
        _suppressRowChanges = false;

        if (changed.IsNewProduct && changed.NewProductCorrelationId.HasValue &&
            _newProducts.TryGetValue(changed.Barcode, out var product) &&
            changed.CostPrice.HasValue && changed.RegularPrice.HasValue && changed.EmployeePrice.HasValue)
        {
            _newProducts[changed.Barcode] = product with
            {
                CostPrice = changed.CostPrice.Value,
                RegularPrice = changed.RegularPrice.Value,
                EmployeePrice = changed.EmployeePrice.Value
            };
        }
        else if (changed.ProductId.HasValue && changed.ProductVersion.HasValue &&
                 changed.CostPrice.HasValue && changed.RegularPrice.HasValue && changed.EmployeePrice.HasValue)
        {
            if (changed.CostPrice == changed.PreviousCostPrice &&
                changed.RegularPrice == changed.PreviousRegularPrice &&
                changed.EmployeePrice == changed.PreviousEmployeePrice)
                _priceUpdates.Remove(changed.ProductId.Value);
            else
                _priceUpdates[changed.ProductId.Value] = new BatchReceiptPriceUpdateRequest(
                    changed.ProductId.Value,
                    changed.CostPrice.Value,
                    changed.RegularPrice.Value,
                    changed.EmployeePrice.Value,
                    changed.ProductVersion.Value);
        }
        EditableDraftChanged();
    }

    private void PublishPriceChangeNotifications(IReadOnlyList<BatchReceiptPriceChangeResponse> changes)
    {
        foreach (var change in changes)
        {
            var details = new List<string>();
            AddPriceChange(details, "Cost", change.PreviousCostPrice, change.CostPrice);
            AddPriceChange(details, "Selling", change.PreviousRegularPrice, change.RegularPrice);
            AddPriceChange(details, "Employee", change.PreviousEmployeePrice, change.EmployeePrice);
            if (details.Count > 0)
                _notifications.ShowWarning($"Prices updated: {change.ProductName}", string.Join(Environment.NewLine, details));
        }
    }

    private static void AddPriceChange(ICollection<string> details, string label, decimal previous, decimal latest)
    {
        if (previous == latest) return;
        var direction = latest > previous ? "increased" : "decreased";
        details.Add($"{label} {direction}: ₱{previous:N2} -> ₱{latest:N2}");
    }

    private void AddIssue(string severity, int? sourceRecord, string field, string code, string message) =>
        Issues.Add(new BatchReceivingDisplayIssue(severity, sourceRecord is null ? "Batch" : $"Record {sourceRecord}", field, code, message));

    private void NotifyActions()
    {
        OnPropertyChanged(nameof(CanReview));
        OnPropertyChanged(nameof(CanCommit));
    }

    private void SetCommitAttempted(bool value)
    {
        if (_commitAttempted == value) return;
        _commitAttempted = value;
        OnPropertyChanged(nameof(CanReview));
    }

    private void RefreshStateProperties()
    {
        OnPropertyChanged(nameof(HasPreview));
        OnPropertyChanged(nameof(HasIssues));
        NotifyActions();
    }

    private void NotifyDeliveryTimeChanged()
    {
        OnPropertyChanged(nameof(IsManualDeliveryTime));
        OnPropertyChanged(nameof(DeliveryAtUtc));
        OnPropertyChanged(nameof(DeliveryTimePreview));
        OnPropertyChanged(nameof(DeliveryTimeConfirmationText));
        OnPropertyChanged(nameof(DeliveryTimeHelpText));
    }

    private static string Fingerprint(BatchReceiptRequest request) =>
        Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(request)));

    private static string? NullIfWhiteSpace(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string Plural(int count) => count == 1 ? "" : "s";
    private void ShowError(string title, string message)
    {
        StatusMessage = message;
        _notifications.ShowError(title, message);
    }
    private static bool IsApiFailure(Exception exception) => exception is ApiClientException or HttpRequestException or TaskCanceledException or InvalidOperationException;
    private static string FailureMessage(Exception exception) => exception is HttpRequestException
        ? "Cannot reach the store API."
        : exception is TaskCanceledException ? "The store API did not respond in time." : exception.Message;
    private static Avalonia.Controls.Window? MainWindow() =>
        Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow: { } window } ? window : null;
}
