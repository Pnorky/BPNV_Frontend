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

public partial class BatchReceivingViewModel : ObservableObject
{
    private readonly StoreApiClient _api;
    private readonly INotificationService _notifications;
    private readonly EyoyoBatchReceiveParser _parser;
    private BatchReceiptRequest? _validatedRequest;
    private string? _validatedFingerprint;
    private bool _suppressDraftChanges;
    private bool _commitAttempted;

    [ObservableProperty] private string _captureText = "";
    [ObservableProperty] private string _reference = "";
    [ObservableProperty] private string _notes = "";
    [ObservableProperty] private string _statusMessage = "Focus the capture field, run Eyoyo Keyboard Export, then review the batch.";
    [ObservableProperty] private string _validationSummary = "Not reviewed";
    [ObservableProperty] private string? _previewError;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _backendCanCommit;
    [ObservableProperty] private BatchReceiptResponse? _result;
    [ObservableProperty] private IReadOnlyList<BatchReceiptPreviewRowResponse> _previewRows = [];

    public ObservableCollection<BatchReceivingDisplayIssue> Issues { get; } = [];
    public Guid IdempotencyKey { get; private set; } = Guid.NewGuid();
    public bool CanReview => !IsBusy && !_commitAttempted && !string.IsNullOrWhiteSpace(CaptureText);
    public bool CanCommit => !IsBusy && BackendCanCommit && _validatedRequest is not null;
    public bool CanEdit => !IsBusy;
    public bool HasPreview => PreviewRows.Count > 0;
    public bool HasIssues => Issues.Count > 0;
    public bool HasResult => Result is not null;

    public BatchReceivingViewModel(
        StoreApiClient api,
        INotificationService notifications,
        EyoyoBatchReceiveParser? parser = null)
    {
        _api = api;
        _notifications = notifications;
        _parser = parser ?? new EyoyoBatchReceiveParser();
    }

    partial void OnCaptureTextChanged(string value) => DraftChanged();
    partial void OnReferenceChanged(string value) => DraftChanged();
    partial void OnNotesChanged(string value) => DraftChanged();
    partial void OnIsBusyChanged(bool value)
    {
        OnPropertyChanged(nameof(CanEdit));
        NotifyActions();
    }
    partial void OnBackendCanCommitChanged(bool value) => OnPropertyChanged(nameof(CanCommit));
    partial void OnResultChanged(BatchReceiptResponse? value) => OnPropertyChanged(nameof(HasResult));
    partial void OnPreviewRowsChanged(IReadOnlyList<BatchReceiptPreviewRowResponse> value) => OnPropertyChanged(nameof(HasPreview));

    [RelayCommand]
    public async Task ReviewBatchAsync()
    {
        if (!CanReview)
        {
            if (_commitAttempted)
                StatusMessage = "The previous receipt result is uncertain. Retry the exact batch with Receive, or edit or clear the draft to start with a new key.";
            return;
        }
        InvalidatePreview(clearResult: true);

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

        var request = BuildRequest(parsed.Records);
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

            PreviewRows = response.Rows.ToArray();
            foreach (var issue in response.Issues) AddIssue(issue.Severity, issue.SourceRecord, issue.Field, issue.Code, issue.Message);
            var summary = response.Summary;
            ValidationSummary = $"{summary.InputRecordCount:N0} input records, {summary.NormalizedLineCount:N0} preview lines, " +
                $"{summary.AffectedProductCount:N0} products, {summary.TotalBasePieces?.ToString("N0") ?? "-"} base pieces, " +
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
            $"This will atomically receive {PreviewRows.Count:N0} validated line{Plural(PreviewRows.Count)} into Bodega. No stock will be added to Display.",
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
    public async Task ClearDraftAsync()
    {
        if (IsBusy) return;
        if (HasDraftData())
        {
            var window = MainWindow();
            if (window is null) return;
            var dialog = new ConfirmDialog();
            dialog.SetConfirmation("Clear batch draft?", "This removes the raw scanner capture, preview, reference, notes, and successful result.", "Clear draft");
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
        records);

    private void DraftChanged()
    {
        if (_suppressDraftChanges) return;
        if (_commitAttempted)
        {
            IdempotencyKey = Guid.NewGuid();
            SetCommitAttempted(false);
            OnPropertyChanged(nameof(IdempotencyKey));
        }
        InvalidatePreview(clearResult: true);
        StatusMessage = "Draft changed. Select Review Batch to validate the current capture.";
        NotifyActions();
    }

    private void InvalidatePreview(bool clearResult)
    {
        BackendCanCommit = false;
        _validatedRequest = null;
        _validatedFingerprint = null;
        PreviewError = null;
        PreviewRows = [];
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
        _suppressDraftChanges = false;
        PreviewRows = [];
        Issues.Clear();
        PreviewError = null;
        ValidationSummary = "Receipt completed";
        BackendCanCommit = false;
        _validatedRequest = null;
        _validatedFingerprint = null;
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
        _suppressDraftChanges = false;
        SetCommitAttempted(false);
        IdempotencyKey = Guid.NewGuid();
        OnPropertyChanged(nameof(IdempotencyKey));
        InvalidatePreview(clearResult: true);
        NotifyActions();
    }

    private bool HasDraftData() =>
        !string.IsNullOrEmpty(CaptureText) || !string.IsNullOrWhiteSpace(Reference) || !string.IsNullOrWhiteSpace(Notes) ||
        PreviewRows.Count > 0 || Issues.Count > 0 || Result is not null;

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
            InvalidatePreview(clearResult: true);
            StatusMessage = "The draft changed after validation. Review it again before receiving stock.";
            return false;
        }

        return true;
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

    private static string Fingerprint(BatchReceiptRequest request) =>
        Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(request)));

    private static string? NullIfWhiteSpace(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string Plural(int count) => count == 1 ? "" : "s";
    private static bool IsApiFailure(Exception exception) => exception is ApiClientException or HttpRequestException or TaskCanceledException or InvalidOperationException;
    private static string FailureMessage(Exception exception) => exception is HttpRequestException
        ? "Cannot reach the store API."
        : exception is TaskCanceledException ? "The store API did not respond in time." : exception.Message;
    private static Avalonia.Controls.Window? MainWindow() =>
        Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow: { } window } ? window : null;
}
