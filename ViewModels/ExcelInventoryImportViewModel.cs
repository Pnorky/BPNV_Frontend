using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using AvaloniaApp.Services;
using AvaloniaApp.Views.Dialogs;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AvaloniaApp.ViewModels;

public partial class InventoryImportSectionMapping(ExcelInventorySectionDraft section) : ObservableObject
{
    public ExcelInventorySectionDraft Section { get; } = section;
    public string Heading => Section.Heading;
    public string SourceDisplay => $"{Section.SourceSheet}, row {Section.SourceRow}";

    [ObservableProperty] private string _supplierName = section.SuggestedSupplierName ?? "";
    [ObservableProperty] private string _category = section.SuggestedCategory ?? section.Heading;
    [ObservableProperty] private ApiInventoryItemType _itemType = section.SuggestedItemType ?? ApiInventoryItemType.Merchandise;
}

public sealed record InventoryImportDisplayIssue(string Severity, string Location, string Field, string Message);

public partial class ExcelInventoryImportViewModel : ObservableObject
{
    private readonly StoreApiClient _api;
    private readonly INotificationService _notifications;
    private readonly ExcelInventoryImportService _excel = new();
    private ExcelInventoryImportResult? _draft;
    private IReadOnlyList<SupplierResponse> _existingSuppliers = [];
    private InventoryImportRequest? _validatedRequest;
    private string? _validatedFingerprint;

    [ObservableProperty] private string _fileName = "No workbook loaded";
    [ObservableProperty] private string _formatDisplay = "-";
    [ObservableProperty] private string _sourceHash = "-";
    [ObservableProperty] private string _statusMessage = "Open a legacy inventory workbook or the BPNV standard template to begin.";
    [ObservableProperty] private string _validationSummary = "Not validated";
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _isLoaded;
    [ObservableProperty] private bool _backendValidated;

    [ObservableProperty] private string _defaultSupplierName = "";
    [ObservableProperty] private string _defaultCategory = "General";
    [ObservableProperty] private string _defaultUnit = "piece";
    [ObservableProperty] private ApiInventoryItemType _defaultItemType = ApiInventoryItemType.Merchandise;
    [ObservableProperty] private decimal _defaultCostPrice;
    [ObservableProperty] private decimal _defaultRegularPrice;
    [ObservableProperty] private decimal _defaultEmployeePrice;
    [ObservableProperty] private decimal _defaultCriticalLevel;
    [ObservableProperty] private decimal _defaultCriticalOrderQuantity = 1;
    [ObservableProperty] private decimal _defaultWarningLevel = 1;
    [ObservableProperty] private decimal _defaultWarningOrderQuantity = 1;

    public ObservableCollection<ExcelInventoryProductDraft> Products { get; } = [];
    public ObservableCollection<ExcelInventoryPackageDraft> Packages { get; } = [];
    public ObservableCollection<InventoryImportSectionMapping> Sections { get; } = [];
    public ObservableCollection<InventoryImportDisplayIssue> Issues { get; } = [];
    public IReadOnlyList<ApiInventoryItemType> ItemTypes { get; } = Enum.GetValues<ApiInventoryItemType>();
    public Guid ImportKey { get; private set; }
    public int ProductCount => Products.Count;
    public int PackageCount => Packages.Count;
    public int SectionCount => Sections.Count;
    public int IssueCount => Issues.Count;
    public bool CanImport => IsLoaded && BackendValidated && !IsBusy;

    public ExcelInventoryImportViewModel(StoreApiClient api, INotificationService notifications)
    {
        _api = api;
        _notifications = notifications;
    }

    partial void OnIsBusyChanged(bool value) => OnPropertyChanged(nameof(CanImport));
    partial void OnBackendValidatedChanged(bool value) => OnPropertyChanged(nameof(CanImport));
    partial void OnIsLoadedChanged(bool value) => OnPropertyChanged(nameof(CanImport));

    [RelayCommand]
    private async Task OpenWorkbookAsync()
    {
        var window = MainWindow();
        if (window is null || IsBusy) return;
        var files = await window.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open inventory workbook",
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("Excel workbook") { Patterns = ["*.xlsx"] }]
        });
        var file = files.FirstOrDefault();
        if (file is null) return;

        IsBusy = true;
        StatusMessage = "Reading and checking workbook...";
        try
        {
            await using var input = await file.OpenReadAsync();
            using var bytes = new MemoryStream();
            await input.CopyToAsync(bytes);
            var data = bytes.ToArray();
            var hash = SHA256.HashData(data);
            SourceHash = Convert.ToHexString(hash).ToLowerInvariant();
            ImportKey = StableGuid(hash);
            FileName = file.Name;
            _draft = _excel.Parse(new MemoryStream(data));
            _existingSuppliers = await _api.GetSuppliersAsync();
            LoadDraft(_draft);
            StatusMessage = $"Loaded {Products.Count} product row{Plural(Products.Count)}. Complete mappings and defaults, then validate.";
        }
        catch (Exception exception)
        {
            ClearDraft();
            StatusMessage = $"Could not load the workbook: {exception.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void ApplySection(InventoryImportSectionMapping? mapping)
    {
        if (mapping is null) return;
        foreach (var product in Products.Where(product => ReferenceEquals(product.Section, mapping.Section)))
        {
            product.SupplierName = mapping.SupplierName.Trim();
            product.Category = mapping.Category.Trim();
            product.ItemType = mapping.ItemType;
        }
        InvalidateValidation($"Applied section '{mapping.Heading}'.");
        RefreshProducts();
        RecheckLocal();
    }

    [RelayCommand]
    private void ApplyAllSections()
    {
        foreach (var mapping in Sections) ApplySectionValues(mapping);
        InvalidateValidation("Applied all section mappings.");
        RefreshProducts();
        RecheckLocal();
    }

    [RelayCommand]
    private void ApplyDefaults()
    {
        foreach (var product in Products)
        {
            if (string.IsNullOrWhiteSpace(product.SupplierName)) product.SupplierName = DefaultSupplierName.Trim();
            if (product.ItemType is null) product.ItemType = DefaultItemType;
            if (string.IsNullOrWhiteSpace(product.Sku)) product.Sku = SuggestedSku(product);
            if (string.IsNullOrWhiteSpace(product.Category)) product.Category = DefaultCategory.Trim();
            if (string.IsNullOrWhiteSpace(product.Unit)) product.Unit = DefaultUnit.Trim();
            product.CostPrice ??= DefaultCostPrice;
            product.RegularPrice ??= DefaultRegularPrice;
            product.EmployeePrice ??= DefaultEmployeePrice;
            product.CriticalReorderLevel ??= Whole(DefaultCriticalLevel);
            product.CriticalOrderQuantity ??= Whole(DefaultCriticalOrderQuantity);
            product.WarningReorderLevel ??= Whole(DefaultWarningLevel);
            product.WarningOrderQuantity ??= Whole(DefaultWarningOrderQuantity);
            product.OpeningDisplayStock ??= 0;
            product.OpeningBodegaStock ??= 0;
        }
        InvalidateValidation("Filled missing product fields with the bulk defaults. Generated SKUs use the source row and product name.");
        RefreshProducts();
        RecheckLocal();
    }

    [RelayCommand]
    private void RecheckLocal()
    {
        InvalidateValidation("Local checks refreshed. Run backend validation when there are no local errors.");
        TryBuildRequest(out _, out _);
    }

    [RelayCommand]
    private async Task ExportBlankTemplateAsync() => await ExportTemplateAsync(null, "BPNV-inventory-import-template.xlsx");

    [RelayCommand]
    private async Task ExportPrefilledTemplateAsync()
    {
        if (_draft is null)
        {
            StatusMessage = "Load a workbook before exporting a prefilled template.";
            return;
        }
        await ExportTemplateAsync(_draft, $"{Path.GetFileNameWithoutExtension(FileName)}-standard.xlsx");
    }

    [RelayCommand]
    private async Task ValidateAsync()
    {
        if (IsBusy) return;
        if (!TryBuildRequest(out var request, out var error))
        {
            if (!string.IsNullOrWhiteSpace(error)) StatusMessage = error;
            return;
        }

        IsBusy = true;
        StatusMessage = "Validating against existing suppliers, products, SKUs, and barcodes...";
        try
        {
            var result = await _api.ValidateInventoryImportAsync(request!);
            ShowBackendResult(result);
            BackendValidated = result.IsValid;
            _validatedRequest = result.IsValid ? request : null;
            _validatedFingerprint = result.IsValid ? Fingerprint(request!) : null;
            StatusMessage = result.IsValid
                ? "Backend validation passed. Review the summary, then import."
                : $"Backend validation found {result.Issues.Count} issue{Plural(result.Issues.Count)}.";
            if (result.IsValid)
                _notifications.ShowSuccess("Validation passed", StatusMessage);
            else
                _notifications.ShowWarning("Validation issues found", StatusMessage);
        }
        catch (Exception exception) when (IsApiFailure(exception))
        {
            BackendValidated = false;
            StatusMessage = $"Validation failed: {exception.Message}";
            _notifications.ShowError("Validation failed", StatusMessage);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ImportAsync()
    {
        if (IsBusy) return;
        if (_validatedRequest is null)
        {
            StatusMessage = "Run backend validation before importing.";
            return;
        }
        if (!TryBuildRequest(out var current, out var error))
        {
            if (!string.IsNullOrWhiteSpace(error)) StatusMessage = error;
            return;
        }
        if (_validatedFingerprint != Fingerprint(current!))
        {
            InvalidateValidation("The draft changed after validation. Validate it again before importing.");
            return;
        }

        var window = MainWindow();
        if (window is null) return;
        var dialog = new ConfirmDialog();
        dialog.SetConfirmation(
            "Import inventory?",
            $"This will create {ProductCount} products and apply their opening Display and Bodega balances. This action cannot be undone.",
            "Import inventory");
        await dialog.ShowDialog(window);
        if (!dialog.Confirmed) return;

        IsBusy = true;
        StatusMessage = "Importing inventory...";
        try
        {
            var result = await _api.ImportInventoryAsync(_validatedRequest);
            ShowBackendResult(result.Validation);
            BackendValidated = result.Validation.IsValid;
            StatusMessage = result.Committed
                ? $"Inventory import completed successfully. Import key: {result.ImportKey}"
                : "The import was not committed. Resolve the reported issues and validate again.";
            if (result.Committed)
            {
                _notifications.ShowSuccess("Inventory imported", StatusMessage);
                _validatedRequest = null;
                BackendValidated = false;
            }
            else
            {
                _notifications.ShowWarning("Inventory not imported", StatusMessage);
            }
        }
        catch (Exception exception) when (IsApiFailure(exception))
        {
            BackendValidated = false;
            StatusMessage = $"Import failed: {exception.Message}";
            _notifications.ShowError("Inventory import failed", StatusMessage);
        }
        finally
        {
            IsBusy = false;
        }
    }

    internal bool TryBuildRequest(out InventoryImportRequest? request, out string error)
    {
        request = null;
        error = "";
        Issues.Clear();
        if (_draft is null)
        {
            error = "Open an inventory workbook first.";
            return false;
        }

        foreach (var issue in _draft.Issues)
            Issues.Add(new InventoryImportDisplayIssue(
                issue.Severity == ExcelInventoryIssueSeverity.Error ? "Error" : "Warning",
                Location(issue.SourceSheet, issue.SourceRow),
                issue.Code,
                issue.Message));

        if (Products.Count is < 1 or > 1000) AddLocal(null, "products", "The import must contain between 1 and 1,000 product rows.");
        var sourceRows = new HashSet<int>();
        var skus = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var productNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var product in Products)
        {
            Required(product, product.SupplierName, "supplier", "Supplier is required.");
            Required(product, product.Sku, "sku", "SKU is required.");
            Required(product, product.Name, "name", "Product name is required.");
            Required(product, product.Category, "category", "Category is required.");
            Required(product, product.Unit, "unit", "Unit is required.");
            if (product.ItemType is null) AddLocal(product.SourceRow, "itemType", "Item type is required.");
            RequiredNumber(product, product.CostPrice, "costPrice");
            RequiredNumber(product, product.RegularPrice, "regularPrice");
            RequiredNumber(product, product.EmployeePrice, "employeePrice");
            NonNegative(product, product.OpeningDisplayStock, "openingDisplayStock");
            NonNegative(product, product.OpeningBodegaStock, "openingBodegaStock");
            NonNegative(product, product.CriticalReorderLevel, "criticalReorderLevel");
            Positive(product, product.CriticalOrderQuantity, "criticalOrderQuantity");
            NonNegative(product, product.WarningReorderLevel, "warningReorderLevel");
            Positive(product, product.WarningOrderQuantity, "warningOrderQuantity");
            if (product.CriticalReorderLevel is not null && product.WarningReorderLevel <= product.CriticalReorderLevel)
                AddLocal(product.SourceRow, "warningReorderLevel", "Warning reorder level must be greater than critical reorder level.");
            if (!sourceRows.Add(product.SourceRow)) AddLocal(product.SourceRow, "sourceRow", "Source row is duplicated.");
            if (!string.IsNullOrWhiteSpace(product.Sku) && !skus.Add(product.Sku.Trim())) AddLocal(product.SourceRow, "sku", "SKU is duplicated in this workbook.");
            if (!string.IsNullOrWhiteSpace(product.SupplierName) && !string.IsNullOrWhiteSpace(product.Name) &&
                !productNames.Add($"{Normalize(product.SupplierName)}\0{product.Name.Trim()}"))
                AddLocal(product.SourceRow, "name", "Product name is duplicated for this supplier.");
        }

        var productSkus = Products.Where(product => !string.IsNullOrWhiteSpace(product.Sku))
            .Select(product => product.Sku.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var package in Packages)
        {
            if (!productSkus.Contains(package.ProductSku.Trim())) AddLocal(package.SourceRow, "package.productSku", "Package ProductSKU does not match a product row.");
            if (string.IsNullOrWhiteSpace(package.Label)) AddLocal(package.SourceRow, "package.label", "Package label is required.");
            if (package.PiecesPerUnit is null or <= 1) AddLocal(package.SourceRow, "package.piecesPerUnit", "Package pieces per unit must be greater than one.");
            if (package.RegularPrice is null or < 0) AddLocal(package.SourceRow, "package.regularPrice", "Package regular price is required and cannot be negative.");
            if (package.EmployeePrice is null or < 0) AddLocal(package.SourceRow, "package.employeePrice", "Package employee price is required and cannot be negative.");
        }

        OnPropertyChanged(nameof(IssueCount));
        if (Issues.Any(issue => issue.Severity == "Error"))
        {
            error = $"Resolve {Issues.Count(issue => issue.Severity == "Error")} local error{Plural(Issues.Count(issue => issue.Severity == "Error"))} before backend validation.";
            ValidationSummary = "Local validation failed";
            return false;
        }

        var suppliers = BuildSuppliers();
        var supplierKeys = suppliers.ToDictionary(supplier => Normalize(supplier.Name), supplier => supplier.Key);
        var products = Products.Select(product => new InventoryImportProductRequest(
            product.SourceRow,
            supplierKeys[Normalize(product.SupplierName)],
            product.ItemType!.Value,
            product.Sku.Trim(),
            NullIfWhiteSpace(product.PieceBarcode),
            product.Name.Trim(),
            product.Category.Trim(),
            product.Unit.Trim(),
            product.CostPrice!.Value,
            product.RegularPrice!.Value,
            product.EmployeePrice!.Value,
            product.CriticalReorderLevel!.Value,
            product.CriticalOrderQuantity!.Value,
            product.WarningReorderLevel!.Value,
            product.WarningOrderQuantity!.Value,
            product.OpeningDisplayStock!.Value,
            product.OpeningBodegaStock!.Value,
            Packages.Where(package => string.Equals(package.ProductSku.Trim(), product.Sku.Trim(), StringComparison.OrdinalIgnoreCase))
                .Select(package => new InventoryImportPackageRequest(
                    NullIfWhiteSpace(package.Barcode), package.Label.Trim(), package.PiecesPerUnit!.Value,
                    package.RegularPrice!.Value, package.EmployeePrice!.Value, package.IsActive)).ToArray())).ToArray();
        request = new InventoryImportRequest(ImportKey, FileName, SourceHash, suppliers, products);
        ValidationSummary = $"Local checks passed: {products.Length} products, {suppliers.Length} suppliers, {Packages.Count} packages";
        return true;
    }

    private InventoryImportSupplierRequest[] BuildSuppliers()
    {
        var sourceSuppliers = (_draft?.Suppliers ?? []).Where(supplier => !string.IsNullOrWhiteSpace(supplier.Name))
            .GroupBy(supplier => Normalize(supplier.Name)).ToDictionary(group => group.Key, group => group.First());
        return Products.Select(product => product.SupplierName.Trim())
            .Where(name => name.Length > 0)
            .GroupBy(Normalize)
            .Select(group =>
            {
                var normalized = group.Key;
                var existing = _existingSuppliers.FirstOrDefault(supplier => Normalize(supplier.Name) == normalized);
                sourceSuppliers.TryGetValue(normalized, out var source);
                var name = existing?.Name ?? source?.Name.Trim() ?? group.First();
                return new InventoryImportSupplierRequest(
                    SupplierKey(normalized), name, existing is null,
                    NullIfWhiteSpace(source?.ContactPerson), NullIfWhiteSpace(source?.Phone));
            }).ToArray();
    }

    private void LoadDraft(ExcelInventoryImportResult draft)
    {
        Products.Clear();
        foreach (var product in draft.Products) Products.Add(product);
        Packages.Clear();
        foreach (var package in draft.Packages) Packages.Add(package);
        Sections.Clear();
        foreach (var section in draft.Sections) Sections.Add(new InventoryImportSectionMapping(section));
        FormatDisplay = draft.Format == ExcelInventoryWorkbookFormat.Legacy ? "Legacy workbook" : "Standard template";
        IsLoaded = true;
        BackendValidated = false;
        ValidationSummary = "Not validated";
        NotifyCounts();
        TryBuildRequest(out _, out _);
    }

    private void ClearDraft()
    {
        _draft = null;
        Products.Clear();
        Packages.Clear();
        Sections.Clear();
        Issues.Clear();
        FileName = "No workbook loaded";
        FormatDisplay = "-";
        SourceHash = "-";
        IsLoaded = false;
        BackendValidated = false;
        ValidationSummary = "Not validated";
        NotifyCounts();
    }

    private async Task ExportTemplateAsync(ExcelInventoryImportResult? source, string suggestedName)
    {
        var window = MainWindow();
        if (window is null || IsBusy) return;
        var file = await window.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = source is null ? "Export blank inventory template" : "Export prefilled standard template",
            SuggestedFileName = suggestedName,
            DefaultExtension = "xlsx",
            FileTypeChoices = [new FilePickerFileType("Excel workbook") { Patterns = ["*.xlsx"] }]
        });
        if (file is null) return;
        try
        {
            await using var output = await file.OpenWriteAsync();
            if (output.CanSeek) output.SetLength(0);
            _excel.WriteTemplate(output, source);
            StatusMessage = source is null ? "Blank standard template exported." : "Prefilled standard template exported with current mappings and defaults.";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            StatusMessage = $"Template export failed: {exception.Message}";
        }
    }

    private void ShowBackendResult(InventoryImportValidationResult result)
    {
        Issues.Clear();
        foreach (var issue in result.Issues)
            Issues.Add(new InventoryImportDisplayIssue("Error", issue.SourceRow is null ? "Import" : $"Row {issue.SourceRow}", issue.Field, issue.Message));
        var summary = result.Summary;
        ValidationSummary = $"{summary.ProductCount} products, {summary.PackageCount} packages, {summary.SupplierCount} suppliers ({summary.SuppliersToCreate} new), {summary.OpeningDisplayQuantity} Display + {summary.OpeningBodegaQuantity} Bodega units";
        OnPropertyChanged(nameof(IssueCount));
    }

    private void ApplySectionValues(InventoryImportSectionMapping mapping)
    {
        foreach (var product in Products.Where(product => ReferenceEquals(product.Section, mapping.Section)))
        {
            product.SupplierName = mapping.SupplierName.Trim();
            product.Category = mapping.Category.Trim();
            product.ItemType = mapping.ItemType;
        }
    }

    private void InvalidateValidation(string message)
    {
        BackendValidated = false;
        _validatedRequest = null;
        _validatedFingerprint = null;
        StatusMessage = message;
    }

    private void RefreshProducts()
    {
        var products = Products.ToArray();
        Products.Clear();
        foreach (var product in products) Products.Add(product);
    }

    private void NotifyCounts()
    {
        OnPropertyChanged(nameof(ProductCount));
        OnPropertyChanged(nameof(PackageCount));
        OnPropertyChanged(nameof(SectionCount));
        OnPropertyChanged(nameof(IssueCount));
    }

    private void Required(ExcelInventoryProductDraft product, string value, string field, string message)
    {
        if (string.IsNullOrWhiteSpace(value)) AddLocal(product.SourceRow, field, message);
    }

    private void RequiredNumber(ExcelInventoryProductDraft product, decimal? value, string field)
    {
        if (value is null || value < 0 || decimal.Round(value.Value, 2) != value)
            AddLocal(product.SourceRow, field, $"{field} is required, cannot be negative, and may have at most two decimals.");
    }

    private void NonNegative(ExcelInventoryProductDraft product, int? value, string field)
    {
        if (value is null or < 0) AddLocal(product.SourceRow, field, $"{field} is required and cannot be negative.");
    }

    private void Positive(ExcelInventoryProductDraft product, int? value, string field)
    {
        if (value is null or <= 0) AddLocal(product.SourceRow, field, $"{field} is required and must be greater than zero.");
    }

    private void AddLocal(int? row, string field, string message) =>
        Issues.Add(new InventoryImportDisplayIssue("Error", row is null ? "Import" : $"Row {row}", field, message));

    private static string Location(string sheet, int? row) => row is null ? sheet : $"{sheet}, row {row}";
    private static int Whole(decimal value) => value is >= int.MinValue and <= int.MaxValue ? decimal.ToInt32(decimal.Truncate(value)) : 0;
    private static string Plural(int count) => count == 1 ? "" : "s";
    private static string? NullIfWhiteSpace(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string Normalize(string value) => new(value.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());
    private static string SupplierKey(string normalizedName) => $"supplier-{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalizedName))).ToLowerInvariant()[..16]}";
    private static string SuggestedSku(ExcelInventoryProductDraft product)
    {
        var name = new string(product.Name.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).Take(18).ToArray());
        return $"IMP-{product.SourceRow}-{(name.Length == 0 ? "ITEM" : name)}";
    }
    private static Guid StableGuid(byte[] hash)
    {
        var bytes = hash[..16];
        bytes[7] = (byte)((bytes[7] & 0x0F) | 0x50);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);
        return new Guid(bytes);
    }
    private static string Fingerprint(InventoryImportRequest request) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(request)));
    private static bool IsApiFailure(Exception exception) => exception is ApiClientException or HttpRequestException or TaskCanceledException;
    private static Avalonia.Controls.Window? MainWindow() =>
        Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow: { } window } ? window : null;
}
