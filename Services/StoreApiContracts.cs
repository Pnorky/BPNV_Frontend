namespace AvaloniaApp.Services;

public enum ApiInventoryItemType
{
    Merchandise,
    Consumable,
    Supply
}

public enum ApiInventoryStockLocation
{
    Display,
    Bodega
}

public enum ApiSpoilageReason
{
    Expired,
    Damaged,
    UnsoldPreparedFood,
    PreparationError,
    Other
}

public enum ApiCustomerType
{
    Regular,
    Employee
}

public enum ApiPaymentMethod
{
    Cash,
    GCash,
    EmployeeOwed
}

public enum ApiCashierShiftSessionStatus
{
    Open,
    ClosedPendingRemittance,
    Reconciled
}

public enum ApiCashierShiftCloseType
{
    CashierClockOut,
    AdministrativeClockOut
}

public enum ApiCashAdjustmentType
{
    CashRefund,
    CashPayout,
    StoreExpense
}

public enum ApiCashAdjustmentStatus
{
    Pending,
    Approved,
    Rejected
}

public sealed record ProductUnitResponse(
    Guid Id,
    string? Barcode,
    string Label,
    int PiecesPerUnit,
    decimal RegularPrice,
    decimal EmployeePrice,
    bool IsBasePiece,
    bool IsActive);

public sealed record ProductResponse(
    Guid Id,
    Guid SupplierId,
    string SupplierName,
    ApiInventoryItemType ItemType,
    string Sku,
    string? Barcode,
    string Name,
    string Category,
    string Unit,
    decimal CostPrice,
    decimal RegularPrice,
    decimal EmployeePrice,
    int CriticalReorderLevel,
    int CriticalOrderQuantity,
    int WarningReorderLevel,
    int WarningOrderQuantity,
    int DisplayStock,
    int BodegaStock,
    int TotalStock,
    bool IsLowStock,
    bool IsCriticalStock,
    int SuggestedOrderQuantity,
    ulong Version,
    bool IsActive,
    IReadOnlyList<ProductUnitResponse> Units,
    bool IsSellable = true,
    bool IsPerishable = false,
    string SalesReportCategory = SalesReportCategories.Other)
{
    public string ActivityStatus => IsActive ? "Active" : "Inactive";
    public bool IsInactive => !IsActive;
    public string StockStatus => TotalStock == 0 ? "Out of stock" : IsCriticalStock ? "Critical" : IsLowStock ? "Warning" : "In stock";
    public string ReorderActionDisplay => SuggestedOrderQuantity > 0
        ? $"Order {SuggestedOrderQuantity} pieces"
        : "No order needed";
    public string ReorderRulesDisplay => $"Critical ≤ {CriticalReorderLevel}: {CriticalOrderQuantity} · Warning ≤ {WarningReorderLevel}: {WarningOrderQuantity}";
    public string PurchasePriceDisplay => $"₱{CostPrice:N2}";
    public string SellingPriceDisplay => $"₱{RegularPrice:N2}";
    public string EmployeePriceDisplay => $"₱{(EmployeePrice > 0 ? EmployeePrice : RegularPrice):N2}";
    public string StockDisplay => $"{DisplayStock} display / {BodegaStock} bodega";
    public string BarcodeDisplay => string.IsNullOrWhiteSpace(Barcode) ? "No barcode (optional)" : Barcode;
    public bool CanRecordStockCount => IsActive && !IsPerishable && ItemType != ApiInventoryItemType.Merchandise;
    public string HandlingDisplay => string.Join(" · ", new[]
    {
        IsSellable ? "Sellable" : "Internal",
        IsPerishable ? "Perishable" : null
    }.Where(value => value is not null));
}

public sealed record SupplierResponse(
    Guid Id,
    string Name,
    string? ContactPerson,
    string? Phone,
    bool IsActive)
{
    public string Details => string.Join(" · ", new[] { ContactPerson, Phone }.Where(value => !string.IsNullOrWhiteSpace(value)));
    public string Status => IsActive ? "Active" : "Inactive";
    public override string ToString() => Name;
}

public sealed record EmployeeResponse(Guid Id, string EmployeeNumber, string Name, bool IsActive)
{
    public string Status => IsActive ? "Active" : "Inactive";
    public string SearchText => $"{EmployeeNumber} {Name}";
    public override string ToString() => $"{EmployeeNumber} · {Name}";
}

public sealed record CreateEmployeeRequest(string Name);
public sealed record UpdateEmployeeRequest(string Name);

public sealed record UserResponse(
    Guid Id,
    string Username,
    string DisplayName,
    bool IsActive,
    bool MustChangePassword,
    IReadOnlyList<string> Roles)
{
    public string RoleDisplay => string.Join(" / ", Roles);
    public string Status => IsActive ? "Active" : "Inactive";
}

public sealed record CreateUserRequest(string Username, string DisplayName, string Password, IReadOnlyList<string> Roles);
public sealed record UpdateUserRequest(string Username, string DisplayName, bool IsActive, string? Password, IReadOnlyList<string> Roles);

public sealed record PosProductResponse(
    Guid Id,
    string SupplierName,
    string Sku,
    string? Barcode,
    string Name,
    string Unit,
    decimal RegularPrice,
    decimal EmployeePrice,
    int DisplayStock,
    ulong Version,
    IReadOnlyList<ProductUnitResponse> Units,
    ProductUnitResponse? SelectedUnit,
    bool IsPerishable = false)
{
    public string CatalogDetails => $"{Sku} · {SupplierName} · Display: {DisplayStock} {Unit}";
}

public sealed record CreateSupplierRequest(string Name, string? ContactPerson, string? Phone);
public sealed record UpdateSupplierRequest(string Name, string? ContactPerson, string? Phone);

public sealed record CreateProductUnitRequest(
    string Barcode,
    string Label,
    int PiecesPerUnit,
    decimal RegularPrice,
    decimal EmployeePrice,
    bool IsActive = true);

public sealed record CreateProductRequest(
    Guid SupplierId,
    ApiInventoryItemType ItemType,
    string Sku,
    string? PieceBarcode,
    string Name,
    string Category,
    string Unit,
    decimal CostPrice,
    decimal RegularPrice,
    decimal EmployeePrice,
    int CriticalReorderLevel,
    int CriticalOrderQuantity,
    int WarningReorderLevel,
    int WarningOrderQuantity,
    IReadOnlyList<CreateProductUnitRequest>? Packages,
    bool? IsSellable = null,
    bool IsPerishable = false,
    string? SalesReportCategory = null);

public sealed record UpdateProductUnitRequest(
    Guid? Id,
    string Barcode,
    string Label,
    int PiecesPerUnit,
    decimal RegularPrice,
    decimal EmployeePrice,
    bool IsActive);

public sealed record UpdateProductRequest(
    Guid SupplierId,
    ApiInventoryItemType ItemType,
    string Sku,
    string? PieceBarcode,
    string Name,
    string Category,
    string Unit,
    decimal CostPrice,
    decimal RegularPrice,
    decimal EmployeePrice,
    int CriticalReorderLevel,
    int CriticalOrderQuantity,
    int WarningReorderLevel,
    int WarningOrderQuantity,
    ulong Version,
    IReadOnlyList<UpdateProductUnitRequest>? Packages,
    bool? IsSellable = null,
    bool? IsPerishable = null,
    string? SalesReportCategory = null);

public sealed record ReplaceBarcodeRequest(bool Confirmed);

public sealed record BarcodeLabelDataResponse(
    Guid ProductId,
    Guid UnitId,
    string ProductName,
    string UnitLabel,
    string Sku,
    string Barcode,
    decimal RegularPrice);

public sealed record InventoryImportSupplierRequest(
    string Key,
    string Name,
    bool CreateIfMissing,
    string? ContactPerson,
    string? Phone);

public sealed record InventoryImportPackageRequest(
    string? Barcode,
    string Label,
    int PiecesPerUnit,
    decimal RegularPrice,
    decimal EmployeePrice,
    bool IsActive = true);

public sealed record InventoryImportProductRequest(
    int SourceRow,
    string SupplierKey,
    ApiInventoryItemType ItemType,
    string Sku,
    string? PieceBarcode,
    string Name,
    string Category,
    string Unit,
    decimal CostPrice,
    decimal RegularPrice,
    decimal EmployeePrice,
    int CriticalReorderLevel,
    int CriticalOrderQuantity,
    int WarningReorderLevel,
    int WarningOrderQuantity,
    int OpeningDisplayStock,
    int OpeningBodegaStock,
    IReadOnlyList<InventoryImportPackageRequest>? Packages,
    bool? IsSellable = null,
    bool IsPerishable = false,
    string? SalesReportCategory = null);

public sealed record InventoryImportRequest(
    Guid ImportKey,
    string SourceFileName,
    string SourceHash,
    IReadOnlyList<InventoryImportSupplierRequest>? Suppliers,
    IReadOnlyList<InventoryImportProductRequest>? Products);

public sealed record InventoryImportIssue(int? SourceRow, string Field, string Code, string Message);

public sealed record InventoryImportSummary(
    int SupplierCount,
    int SuppliersToCreate,
    int ProductCount,
    int PackageCount,
    long OpeningDisplayQuantity,
    long OpeningBodegaQuantity,
    int ErrorCount);

public sealed record InventoryImportValidationResult(
    bool IsValid,
    IReadOnlyList<InventoryImportIssue> Issues,
    InventoryImportSummary Summary);

public sealed record InventoryImportCommitResult(
    bool Committed,
    Guid ImportKey,
    InventoryImportValidationResult Validation);

public sealed record PagedResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);

public sealed record ReceiveStockRequest(
    Guid ProductId,
    Guid UnitId,
    int Count,
    decimal UnitCost,
    decimal RegularPrice,
    decimal EmployeePrice,
    string? Reference,
    string? Notes,
    string? LotCode = null,
    DateTimeOffset? ReceivedAtUtc = null,
    DateTimeOffset? ProductionAtUtc = null,
    DateTimeOffset? ExpiresAtUtc = null);
public sealed record TransferStockRequest(Guid ProductId, int Quantity, string? Reference, string? Notes);
public sealed record RecordStockCountRequest(
    Guid ProductId,
    ApiInventoryStockLocation Location,
    int CountedQuantity,
    ulong ExpectedProductVersion,
    string? Notes);

public sealed record StockReceiptResponse(
    Guid MovementId,
    Guid ProductId,
    Guid UnitId,
    string UnitLabel,
    int Count,
    int PiecesPerUnit,
    int BasePieceQuantity,
    int DisplayStock,
    int BodegaStock,
    ulong ProductVersion,
    DateTime OccurredAtUtc,
    Guid? LotId = null,
    string? LotCode = null,
    DateTimeOffset? ExpiresAtUtc = null)
{
    public string OccurredAtDisplay => StoreDateTime.FormatUtc(OccurredAtUtc);
}

public sealed record BatchReceiptRecordRequest(
    int SourceRecord,
    string? SupplierLibrary,
    string? Barcode,
    int UnitQuantity,
    string? LotCode = null,
    DateTimeOffset? ReceivedAtUtc = null,
    DateTimeOffset? ProductionAtUtc = null,
    DateTimeOffset? ExpiresAtUtc = null);

public sealed record BatchReceiptPriceUpdateRequest(
    Guid ProductId,
    decimal CostPrice,
    decimal RegularPrice,
    decimal EmployeePrice,
    ulong ExpectedProductVersion);

public sealed record BatchReceiptNewProductRequest(
    Guid CorrelationId,
    string ReceiptBarcode,
    Guid SupplierId,
    ApiInventoryItemType ItemType,
    string Sku,
    string? PieceBarcode,
    string Name,
    string Category,
    string Unit,
    decimal CostPrice,
    decimal RegularPrice,
    decimal EmployeePrice,
    int CriticalReorderLevel,
    int CriticalOrderQuantity,
    int WarningReorderLevel,
    int WarningOrderQuantity,
    IReadOnlyList<CreateProductUnitRequest>? Packages,
    bool? IsSellable = null,
    bool IsPerishable = false,
    string? SalesReportCategory = null);

public sealed record BatchReceiptRequest(
    Guid IdempotencyKey,
    string? Reference,
    string? Notes,
    IReadOnlyList<BatchReceiptRecordRequest> Records,
    IReadOnlyList<BatchReceiptPriceUpdateRequest>? PriceUpdates = null,
    IReadOnlyList<BatchReceiptNewProductRequest>? NewProducts = null,
    DateTimeOffset? DeliveryAtUtc = null);

public sealed record BatchReceiptIssueResponse(
    string Code,
    string Field,
    int? SourceRecord,
    string Message,
    string Severity);

public sealed record BatchReceiptPreviewRowResponse(
    IReadOnlyList<int> SourceRecords,
    string SupplierLibrary,
    string Barcode,
    Guid? SupplierId,
    string? SupplierName,
    Guid? ProductId,
    string? ProductName,
    string? Sku,
    Guid? UnitId,
    string? UnitLabel,
    int InputUnitQuantity,
    int? PiecesPerUnit,
    int? BasePieceQuantity,
    int? CurrentBodegaBalance,
    int? ProjectedBodegaBalance,
    string Status,
    IReadOnlyList<BatchReceiptIssueResponse> Issues,
    bool IsNewProduct = false,
    Guid? NewProductCorrelationId = null,
    decimal? PreviousCostPrice = null,
    decimal? CostPrice = null,
    decimal? PreviousRegularPrice = null,
    decimal? RegularPrice = null,
    decimal? PreviousEmployeePrice = null,
    decimal? EmployeePrice = null,
    decimal? TotalCost = null,
    ulong? ProductVersion = null)
{
    public string SourceRecordsDisplay => string.Join(", ", SourceRecords);
    public string SupplierNameDisplay => SupplierName ?? "Unknown";
    public string SupplierResolutionDisplay => $"{SupplierLibrary} -> {SupplierNameDisplay}";
    public string ProductNameDisplay => ProductName ?? "Unknown barcode";
    public string SkuDisplay => Sku ?? "-";
    public string UnitLabelDisplay => UnitLabel ?? "-";
    public string ScannedQuantityDisplay => PiecesPerUnit is > 1
        ? $"{InputUnitQuantity:N0} {UnitLabelDisplay} x {PiecesPerUnit:N0}"
        : string.IsNullOrWhiteSpace(UnitLabel)
            ? InputUnitQuantity.ToString("N0")
            : $"{InputUnitQuantity:N0} {UnitLabel}";
    public string PiecesPerUnitDisplay => PiecesPerUnit?.ToString("N0") ?? "-";
    public string BasePieceQuantityDisplay => BasePieceQuantity?.ToString("N0") ?? "-";
    public string CurrentBodegaDisplay => CurrentBodegaBalance?.ToString("N0") ?? "-";
    public string ProjectedBodegaDisplay => ProjectedBodegaBalance?.ToString("N0") ?? "-";
    public string BodegaChangeDisplay => $"{CurrentBodegaDisplay} -> {ProjectedBodegaDisplay}";
    public string StatusDisplay => Issues.Count == 0 ? Status : $"{Status} ({Issues.Count})";
    public string CostChangeDisplay => PriceChange(PreviousCostPrice, CostPrice);
    public string RegularPriceChangeDisplay => PriceChange(PreviousRegularPrice, RegularPrice);
    public string EmployeePriceChangeDisplay => PriceChange(PreviousEmployeePrice, EmployeePrice);
    private static string PriceChange(decimal? previous, decimal? latest) =>
        previous.HasValue && latest.HasValue && previous != latest ? $"₱{previous:N2} -> ₱{latest:N2}" : $"₱{latest ?? 0:N2}";
}

public sealed record BatchReceiptValidationSummaryResponse(
    int InputRecordCount,
    int NormalizedLineCount,
    int AffectedProductCount,
    int? TotalBasePieces,
    int WarningCount,
    int ErrorCount,
    int IssueCount,
    decimal? TotalCost = null);

public sealed record BatchReceiptValidationResponse(
    Guid IdempotencyKey,
    string? Reference,
    string? Notes,
    bool CanCommit,
    IReadOnlyList<BatchReceiptPreviewRowResponse> Rows,
    IReadOnlyList<BatchReceiptIssueResponse> Issues,
    BatchReceiptValidationSummaryResponse Summary);

public sealed record BatchReceiptResponse(
    Guid BatchId,
    Guid IdempotencyKey,
    string? Reference,
    int AcceptedRecordCount,
    int NormalizedLineCount,
    int AffectedProductCount,
    int TotalBasePieces,
    IReadOnlyList<string> Suppliers,
    DateTime CompletedAtUtc,
    DateTime DeliveryAtUtc,
    bool IsIdempotentReplay,
    decimal TotalCost = 0,
    IReadOnlyList<BatchReceiptPriceChangeResponse>? PriceChanges = null,
    IReadOnlyList<BatchReceiptCreatedProductResponse>? CreatedProducts = null)
{
    public string ReferenceDisplay => string.IsNullOrWhiteSpace(Reference) ? "No receipt number" : Reference;
    public string SuppliersDisplay => string.Join(", ", Suppliers);
    public string CompletedAtDisplay => StoreDateTime.FormatUtc(CompletedAtUtc);
    public string DeliveryAtDisplay => StoreDateTime.FormatUtc(DeliveryAtUtc);
    public string TotalCostDisplay => $"₱{TotalCost:N2}";
    public int CreatedProductCount => CreatedProducts?.Count ?? 0;
}

public sealed record BatchReceiptPriceChangeResponse(
    Guid ProductId,
    string ProductName,
    string Sku,
    decimal PreviousCostPrice,
    decimal CostPrice,
    decimal PreviousRegularPrice,
    decimal RegularPrice,
    decimal PreviousEmployeePrice,
    decimal EmployeePrice);

public sealed record BatchReceiptCreatedProductResponse(
    Guid CorrelationId,
    Guid ProductId,
    string Sku,
    string ProductName,
    string Barcode);

public sealed record DeliveryHistoryItemResponse(
    Guid BatchId,
    string? ReceiptNumber,
    IReadOnlyList<string> Suppliers,
    int AcceptedRecordCount,
    int NormalizedLineCount,
    int AffectedProductCount,
    int TotalBasePieces,
    decimal TotalCost,
    DateTime DeliveryAtUtc,
    DateTime CompletedAtUtc,
    Guid ReceivedByUserId,
    string ReceivedByName,
    string Status)
{
    public string ReceiptNumberDisplay => string.IsNullOrWhiteSpace(ReceiptNumber) ? "No receipt number" : ReceiptNumber;
    public string SuppliersDisplay => Suppliers.Count == 0 ? "Not available" : string.Join(", ", Suppliers);
    public string DeliveryAtDisplay => StoreDateTime.FormatUtc(DeliveryAtUtc);
    public string CompletedAtDisplay => StoreDateTime.FormatUtc(CompletedAtUtc);
    public string ProductsPiecesDisplay => $"{AffectedProductCount:N0} products / {TotalBasePieces:N0} pieces";
    public string TotalCostDisplay => $"₱{TotalCost:N2}";
}

public sealed record DeliveryHistoryLineResponse(
    Guid LineId,
    Guid ProductId,
    Guid ProductUnitId,
    string ProductName,
    string Sku,
    Guid SupplierId,
    string SupplierName,
    string SupplierLibrary,
    string Barcode,
    IReadOnlyList<int> SourceRecords,
    int InputUnitQuantity,
    string UnitLabel,
    int PiecesPerUnit,
    int BasePieceQuantity,
    decimal? PreviousCostPrice,
    decimal CostPrice,
    decimal? PreviousRegularPrice,
    decimal RegularPrice,
    decimal? PreviousEmployeePrice,
    decimal EmployeePrice,
    decimal LineTotal,
    int? BodegaBalanceBefore,
    int? BodegaBalanceAfter,
    bool IsNewProduct)
{
    public string ProductDisplay => $"{ProductName} / {Sku}";
    public string BarcodeUnitDisplay => $"{Barcode} / {UnitLabel}";
    public string SupplierDisplay => string.Equals(SupplierName, SupplierLibrary, StringComparison.OrdinalIgnoreCase)
        ? SupplierName
        : $"{SupplierLibrary} -> {SupplierName}";
    public string ReceivedQuantityDisplay => PiecesPerUnit > 1
        ? $"{InputUnitQuantity:N0} {UnitLabel} x {PiecesPerUnit:N0} = {BasePieceQuantity:N0} pieces"
        : $"{BasePieceQuantity:N0} pieces";
    public string CostPriceDisplay => PriceChange(PreviousCostPrice, CostPrice);
    public string RegularPriceDisplay => PriceChange(PreviousRegularPrice, RegularPrice);
    public string EmployeePriceDisplay => PriceChange(EffectiveEmployeePrice(PreviousEmployeePrice, PreviousRegularPrice), EffectiveEmployeePrice(EmployeePrice, RegularPrice)!.Value);
    public string RegularEmployeePriceDisplay => $"Selling {RegularPriceDisplay}; Employee {EmployeePriceDisplay}";
    public string LineTotalDisplay => $"₱{LineTotal:N2}";
    public string BodegaChangeDisplay => BodegaBalanceBefore.HasValue && BodegaBalanceAfter.HasValue
        ? $"{BodegaBalanceBefore:N0} -> {BodegaBalanceAfter:N0}"
        : "Not available";
    public string NewProductDisplay => IsNewProduct ? "New product" : "Existing product";
    public string LineTotalProductDisplay => $"{LineTotalDisplay} / {NewProductDisplay}";

    private static string PriceChange(decimal? previous, decimal latest) => previous.HasValue
        ? previous.Value == latest ? $"₱{latest:N2}" : $"₱{previous:N2} -> ₱{latest:N2}"
        : $"Not available -> ₱{latest:N2}";

    private static decimal? EffectiveEmployeePrice(decimal? employeePrice, decimal? regularPrice) =>
        employeePrice is > 0 ? employeePrice : regularPrice;
}

public sealed record DeliveryHistoryDetailResponse(
    DeliveryHistoryItemResponse Delivery,
    string? Notes,
    IReadOnlyList<DeliveryHistoryLineResponse> Lines)
{
    public string NotesDisplay => string.IsNullOrWhiteSpace(Notes) ? "No notes" : Notes;
}

public sealed record StockTransferResponse(Guid MovementId, Guid ProductId, int Quantity, int DisplayStock, int BodegaStock, ulong ProductVersion, DateTime OccurredAtUtc)
{
    public string OccurredAtDisplay => StoreDateTime.FormatUtc(OccurredAtUtc);
}

public sealed record StockCountResponse(
    Guid MovementId,
    Guid ProductId,
    ApiInventoryStockLocation Location,
    int PreviousQuantity,
    int CountedQuantity,
    int Variance,
    int DisplayStock,
    int BodegaStock,
    ulong ProductVersion,
    DateTime OccurredAtUtc)
{
    public string OccurredAtDisplay => StoreDateTime.FormatUtc(OccurredAtUtc);
}

public sealed record StockMovementResponse(
    Guid Id,
    Guid ProductId,
    Guid? ProductUnitId,
    Guid? SaleId,
    string ProductName,
    string Sku,
    string SupplierName,
    string MovementType,
    int Quantity,
    int? InputUnitCount,
    string? UnitLabel,
    string? UnitBarcode,
    int? PiecesPerUnit,
    int DisplayDelta,
    int BodegaDelta,
    int DisplayBalanceAfter,
    int BodegaBalanceAfter,
    string? Reference,
    string? Notes,
    DateTime OccurredAtUtc,
    Guid CreatedByUserId,
    string CreatedByName,
    Guid? LotId = null,
    string? LotCode = null,
    ApiSpoilageReason? SpoilageReason = null)
{
    public string OccurredAtDisplay => StoreDateTime.FormatUtc(OccurredAtUtc);
    public string ProductDisplay => $"{ProductName} | {Sku} | {SupplierName}";
    public string MovementTypeDisplay => MovementType switch
    {
        "OpeningDisplay" => "Opening Display",
        "OpeningBodega" => "Opening Bodega",
        "TransferToDisplay" => "Bodega to Display",
        "AccountsReceivable" => "Accounts Receivable",
        "DisplayAdjustmentIn" => "Display Adjustment In",
        "DisplayAdjustmentOut" => "Display Adjustment Out",
        "BodegaAdjustmentIn" => "Bodega Adjustment In",
        "BodegaAdjustmentOut" => "Bodega Adjustment Out",
        "DisplaySpoilage" => "Display Spoilage",
        "BodegaSpoilage" => "Bodega Spoilage",
        "DisplayUsage" => "Display Usage",
        "BodegaUsage" => "Bodega Usage",
        _ => MovementType
    };
    public string UnitDisplay => InputUnitCount.HasValue
        ? $"{InputUnitCount:N0} {UnitLabel ?? "unit"} x {PiecesPerUnit ?? 1}"
        : UnitLabel ?? "Base pieces";
    public string QuantityDisplay => $"{Quantity:N0} pcs";
    public string DisplayDeltaDisplay => Signed(DisplayDelta);
    public string BodegaDeltaDisplay => Signed(BodegaDelta);
    public string BalanceDisplay => $"D {DisplayBalanceAfter:N0} | B {BodegaBalanceAfter:N0}";
    public string ChangeDisplay => $"D {Signed(DisplayDelta)} | B {Signed(BodegaDelta)}";
    public string ReferenceDisplay => string.IsNullOrWhiteSpace(Reference) ? "-" : Reference;
    public string NotesDisplay => string.IsNullOrWhiteSpace(Notes) ? "-" : Notes;
    public string ReferenceNotesDisplay => string.IsNullOrWhiteSpace(Notes)
        ? ReferenceDisplay
        : $"{ReferenceDisplay} | {Notes}";
    public string LotReasonDisplay => string.Join(" · ", new[]
    {
        string.IsNullOrWhiteSpace(LotCode) ? null : $"Lot {LotCode}",
        SpoilageReason?.ToString()
    }.Where(value => value is not null));
    private static string Signed(int value) => value > 0 ? $"+{value:N0}" : value.ToString("N0");
}

public sealed record RecordSpoilageRequest(
    Guid ProductId,
    Guid UnitId,
    ApiInventoryStockLocation Location,
    Guid? LotId,
    int Count,
    ApiSpoilageReason Reason,
    string? Notes);

public sealed record SpoilageResponse(
    Guid MovementId,
    Guid ProductId,
    Guid UnitId,
    Guid? LotId,
    ApiInventoryStockLocation Location,
    int BasePieceQuantity,
    ApiSpoilageReason Reason,
    int DisplayStock,
    int BodegaStock,
    DateTimeOffset OccurredAtUtc);

public sealed record InventoryLotBalanceResponse(
    Guid LotId,
    Guid ProductId,
    Guid UnitId,
    string? LotCode,
    ApiInventoryStockLocation Location,
    int Quantity,
    DateTimeOffset ReceivedAtUtc,
    DateTimeOffset? ProductionAtUtc,
    DateTimeOffset ExpiresAtUtc,
    bool IsExpired,
    bool IsClosed)
{
    public string Display => $"{(string.IsNullOrWhiteSpace(LotCode) ? "Uncoded lot" : LotCode)} · {Quantity:N0} pcs · expires {StoreDateTime.FormatUtc(ExpiresAtUtc.UtcDateTime)}";
    public override string ToString() => Display;
}

public sealed record CreateSaleLineRequest(Guid UnitId, int Count);

public sealed record CreateSaleRequest(
    Guid IdempotencyKey,
    ApiCustomerType CustomerType,
    ApiPaymentMethod PaymentMethod,
    IReadOnlyList<CreateSaleLineRequest> Lines,
    Guid? EmployeeId = null,
    string? Reference = null);

public sealed record SaleLineResponse(
    Guid Id,
    Guid ProductId,
    Guid UnitId,
    string Sku,
    string ProductName,
    string UnitLabel,
    string? UnitBarcode,
    int PiecesPerUnit,
    int Count,
    int BasePieceQuantity,
    decimal UnitPrice,
    decimal LineTotal);

public sealed record SaleResponse(
    Guid Id,
    string SaleNumber,
    Guid IdempotencyKey,
    ApiCustomerType CustomerType,
    ApiPaymentMethod PaymentMethod,
    decimal Subtotal,
    decimal Total,
    DateTime SoldAtUtc,
    Guid SoldByUserId,
    bool IsIdempotentReplay,
    IReadOnlyList<SaleLineResponse> Lines,
    Guid? EmployeeId = null,
    string? EmployeeNumber = null,
    string? EmployeeName = null,
     Guid? ShiftSessionId = null)
{
    public string SoldAtDisplay => StoreDateTime.FormatUtc(SoldAtUtc);
}

public sealed record ReportSaleLineResponse(
    Guid Id,
    Guid ProductId,
    Guid UnitId,
    string Sku,
    string ProductName,
    string UnitLabel,
    string? UnitBarcode,
    int PiecesPerUnit,
    int Count,
    int BasePieceQuantity,
    decimal UnitPrice,
    decimal LineTotal)
{
    public string UnitPriceDisplay => $"₱{UnitPrice:N2}";
    public string LineTotalDisplay => $"₱{LineTotal:N2}";
}

public sealed record ReportSaleResponse(
    Guid Id,
    string SaleNumber,
    ApiCustomerType CustomerType,
    ApiPaymentMethod PaymentMethod,
    decimal Subtotal,
    decimal Total,
    DateTime SoldAtUtc,
    Guid SoldByUserId,
    string SoldByName,
    IReadOnlyList<ReportSaleLineResponse> Lines,
    Guid? EmployeeId = null,
    string? EmployeeNumber = null,
    string? EmployeeName = null,
     Guid? ShiftSessionId = null,
     Guid? ShiftDefinitionId = null,
     string? ShiftName = null)
{
    public int ItemCount => Lines.Sum(line => line.BasePieceQuantity);
    public string PaymentMethodDisplay => PaymentMethod switch
    {
        ApiPaymentMethod.EmployeeOwed => "Employee purchase (owed)",
        ApiPaymentMethod.GCash => "GCash",
        _ => "Cash"
    };
    public string TotalDisplay => $"₱{Total:N2}";
    public string TimeDisplay => StoreDateTime.FormatUtc(SoldAtUtc);
}

public sealed record EmployeePurchaseSummaryResponse(decimal TotalDeductions, decimal TotalOwed, int Transactions, int Employees);

public sealed record EmployeePurchaseLineResponse(
    Guid SaleId,
    string SaleNumber,
    DateTime SoldAtUtc,
    Guid? EmployeeId,
    string? EmployeeNumber,
    string? EmployeeName,
    Guid ProductId,
    string Sku,
    string ProductName,
    string UnitLabel,
    int Count,
    int BasePieceQuantity,
    decimal UnitPrice,
    decimal LineTotal,
    decimal SaleTotal,
    bool IsOwed = false)
{
    public string SoldAtDisplay => StoreDateTime.FormatUtc(SoldAtUtc);
    public string EmployeeDisplay => EmployeeNumber is null ? "Unattributed" : $"{EmployeeNumber} · {EmployeeName}";
    public string QuantityDisplay => $"{Count} {UnitLabel} / {BasePieceQuantity} pieces";
    public string UnitPriceDisplay => $"₱{UnitPrice:N2}";
    public string LineTotalDisplay => $"₱{LineTotal:N2}";
}

public sealed record EmployeePurchaseReportResponse(
    EmployeePurchaseSummaryResponse Summary,
    IReadOnlyList<EmployeePurchaseLineResponse> Lines);

public sealed record TopProductResponse(
    Guid ProductId,
    string Sku,
    string ProductName,
    int Quantity,
    decimal Sales)
{
    public string SalesDisplay => $"₱{Sales:N2}";
}

public sealed record SalesReportSummaryResponse(decimal GrossSales, decimal TodaySales, int Transactions, int UnitsSold);

public sealed record SalesReportResponse(
    SalesReportSummaryResponse Summary,
    IReadOnlyList<TopProductResponse> TopProducts,
    IReadOnlyList<ReportSaleResponse> RecentSales,
    IReadOnlyList<ReportSaleResponse> Sales);

public sealed record InventoryReportProductResponse(
    Guid Id,
    Guid SupplierId,
    string SupplierName,
    ApiInventoryItemType ItemType,
    string Sku,
    string Name,
    string Category,
    string Unit,
    decimal CostPrice,
    decimal RegularPrice,
    decimal EmployeePrice,
    int DisplayStock,
    int BodegaStock,
    int TotalStock,
    int CriticalReorderLevel,
    int CriticalOrderQuantity,
    int WarningReorderLevel,
    int WarningOrderQuantity,
    string ReorderTier,
    int SuggestedOrderQuantity,
    string StockStatus,
    bool IsActive)
{
    public string ItemTypeDisplay => ItemType.ToString();
}

public sealed record InventoryReportSummaryResponse(
    int LowStockItems,
    decimal InventoryValue,
    int DisplayUnits,
    int BodegaUnits,
    int TotalInventoryUnits,
    int MerchandiseCount,
    int ConsumableCount,
    int SupplyCount);

public sealed record InventoryReportResponse(
    InventoryReportSummaryResponse Summary,
    IReadOnlyList<InventoryReportProductResponse> Products);

public sealed record OrderProductResponse(
    Guid ProductId,
    string Sku,
    string ProductName,
    int DisplayStock,
    int BodegaStock,
    int TotalStock,
    int CriticalReorderLevel,
    int WarningReorderLevel,
    string ReorderTier,
    int SuggestedOrderQuantity);

public sealed record SupplierOrderResponse(
    Guid SupplierId,
    string SupplierName,
    int ProductCount,
    int TotalOrderQuantity,
    IReadOnlyList<OrderProductResponse> Products)
{
    public string Summary => $"{ProductCount} product{(ProductCount == 1 ? "" : "s")} · {TotalOrderQuantity} total units";
}

public sealed record OrderReportSummaryResponse(int SuppliersToOrder, int ProductsToOrder, int SuggestedOrderUnits);

public sealed record OrderReportResponse(
    OrderReportSummaryResponse Summary,
    IReadOnlyList<SupplierOrderResponse> Suppliers);

public sealed record DashboardResponse(
    decimal TodaySales,
    int TodayTransactions,
    int DisplayUnits,
    int BodegaUnits,
    IReadOnlyList<InventoryReportProductResponse> AttentionItems,
    IReadOnlyList<ReportSaleResponse> RecentSales);

public sealed record ApiReportSnapshot(
    SalesReportResponse Sales,
    InventoryReportResponse Inventory,
    OrderReportResponse Orders,
    EmployeePurchaseReportResponse? EmployeePurchases = null,
    CashierShiftReportResponse? CashierShifts = null,
    SalesAccountabilityReportResponse? SalesAccountability = null);

public sealed record ShiftDefinitionResponse(Guid Id, string Name, TimeOnly StartLocalTime, TimeOnly EndLocalTime, bool IsActive)
{
    public bool IsOvernight => EndLocalTime <= StartLocalTime;
    public string ScheduleDisplay => $"{StartLocalTime:h:mm tt} - {EndLocalTime:h:mm tt}{(IsOvernight ? " next day" : "")}";
    public string Status => IsActive ? "Active" : "Inactive";
    public override string ToString() => $"{Name} · {ScheduleDisplay}";
}

public sealed record CreateShiftDefinitionRequest(string Name, TimeOnly StartLocalTime, TimeOnly EndLocalTime);
public sealed record UpdateShiftDefinitionRequest(string Name, TimeOnly StartLocalTime, TimeOnly EndLocalTime);

public sealed record CashierShiftScheduleResponse(
    Guid Id, Guid CashierUserId, string CashierName, Guid ShiftDefinitionId, string ShiftName,
    TimeOnly StartLocalTime, TimeOnly EndLocalTime, byte DaysOfWeekMask, DateOnly EffectiveFrom,
    DateOnly? EffectiveTo, bool IsActive)
{
    public string DaysDisplay => CashierShiftFormatting.FormatDays(DaysOfWeekMask);
    public string EffectiveDisplay => EffectiveTo is null ? $"From {EffectiveFrom:MMM d, yyyy}" : $"{EffectiveFrom:MMM d, yyyy} - {EffectiveTo:MMM d, yyyy}";
    public string Status => IsActive ? "Active" : "Inactive";
}

public sealed record CreateCashierShiftScheduleRequest(Guid CashierUserId, Guid ShiftDefinitionId, byte DaysOfWeekMask, DateOnly EffectiveFrom, DateOnly? EffectiveTo);
public sealed record UpdateCashierShiftScheduleRequest(Guid CashierUserId, Guid ShiftDefinitionId, byte DaysOfWeekMask, DateOnly EffectiveFrom, DateOnly? EffectiveTo);

public sealed record CashierShiftOverrideResponse(
    Guid Id, DateOnly BusinessDate, Guid ShiftDefinitionId, string ShiftName, TimeOnly StartLocalTime,
    TimeOnly EndLocalTime, Guid ReplacementCashierUserId, string ReplacementCashierName,
    Guid? ReplacedScheduleId, string Reason);

public sealed record CreateCashierShiftOverrideRequest(DateOnly BusinessDate, Guid ShiftDefinitionId, Guid ReplacementCashierUserId, Guid? ReplacedScheduleId, string Reason);
public sealed record UpdateCashierShiftOverrideRequest(DateOnly BusinessDate, Guid ShiftDefinitionId, Guid ReplacementCashierUserId, Guid? ReplacedScheduleId, string Reason);

public sealed record ResolvedCashierShiftAssignmentResponse(
    Guid? ScheduleId, Guid? AssignmentOverrideId, Guid ShiftDefinitionId, string ShiftName,
    Guid CashierUserId, string CashierName, DateOnly BusinessDate, DateTime ScheduledStartAtUtc,
    DateTime ScheduledEndAtUtc)
{
    public bool IsReplacement => AssignmentOverrideId.HasValue;
    public string SourceDisplay => IsReplacement ? "Replacement" : "Recurring";
    public string ScheduleDisplay => $"{StoreDateTime.FormatUtc(ScheduledStartAtUtc)} - {StoreDateTime.FormatUtc(ScheduledEndAtUtc)}";
}

public sealed record ClockInRequest(Guid IdempotencyKey, decimal OpeningCashFloat);
public sealed record ClockOutRequest(Guid IdempotencyKey);
public sealed record AdministrativeClockOutRequest(Guid IdempotencyKey, string Reason);
public sealed record RecordRemittanceRequest(decimal ActualRemittance, bool CashFloatReturned, string? Note);
public sealed record CreateCashAdjustmentRequest(ApiCashAdjustmentType Type, decimal Amount, string Note, string? Reference);
public sealed record CashAdjustmentReviewRequest(string? Note);
public sealed record CorrectCashAdjustmentRequest(bool Approve, string Reason);
public sealed record CorrectShiftRemittanceRequest(decimal ActualRemittance, bool CashFloatReturned, string Reason);

public sealed record CashierShiftSessionResponse(
    Guid Id, Guid? ScheduleId, Guid? AssignmentOverrideId, Guid ShiftDefinitionId, Guid CashierUserId,
    string CashierName, DateOnly BusinessDate, string ShiftName, DateTime ScheduledStartAtUtc,
    DateTime ScheduledEndAtUtc, DateTime ClockedInAtUtc, DateTime? ClockedOutAtUtc,
    Guid? ClockedOutByUserId, ApiCashierShiftSessionStatus Status, ApiCashierShiftCloseType? CloseType,
    decimal OpeningCashFloat, decimal? ExpectedTerminalCash, int? WorkedMinutes, int ClockInVarianceMinutes,
    int? ClockOutVarianceMinutes, decimal? TotalSales, decimal? CashSales, decimal? GCashSales,
    decimal? CashRefunds, decimal? CashPayouts, int? TransactionCount, decimal? ExpectedRemittance,
    decimal? ActualRemittance, decimal? Variance, bool? CashFloatReturned, string? RemittanceNote,
    DateTime? RemittanceRecordedAtUtc, Guid? RemittanceRecordedByUserId,
    DateTime? CashFloatConfirmedAtUtc, Guid? CashFloatConfirmedByUserId,
    bool IsIdempotentReplay = false)
{
    public string StatusDisplay => Status == ApiCashierShiftSessionStatus.ClosedPendingRemittance ? "Pending Remittance" : Status.ToString();
    public string ScheduledDisplay => $"{StoreDateTime.FormatUtc(ScheduledStartAtUtc)} - {StoreDateTime.FormatUtc(ScheduledEndAtUtc)}";
    public string ActualDisplay => ClockedOutAtUtc is null ? $"{StoreDateTime.FormatUtc(ClockedInAtUtc)} - Active" : $"{StoreDateTime.FormatUtc(ClockedInAtUtc)} - {StoreDateTime.FormatUtc(ClockedOutAtUtc.Value)}";
    public string WorkedDisplay => WorkedMinutes is null
        ? "Active"
        : $"{WorkedMinutes / 60} hour{(WorkedMinutes / 60 == 1 ? "" : "s")} and {WorkedMinutes % 60} minute{(WorkedMinutes % 60 == 1 ? "" : "s")}";
    public string TotalSalesDisplay => CashierShiftFormatting.Money(TotalSales);
    public string ExpectedRemittanceDisplay => CashierShiftFormatting.Money(ExpectedRemittance);
    public string ActualRemittanceDisplay => CashierShiftFormatting.Money(ActualRemittance);
    public string VarianceDisplay => CashierShiftFormatting.SignedMoney(Variance);
}

public sealed record CashierTerminalOccupancyResponse(Guid SessionId, string CashierName, string ShiftName, DateTime ClockedInAtUtc);
public sealed record CashierClockStatusResponse(
    DateTime ServerTimeUtc, DateTimeOffset StoreLocalTime, ResolvedCashierShiftAssignmentResponse? Assignment,
    CashierShiftSessionResponse? OpenSession, CashierTerminalOccupancyResponse? OccupiedTerminal,
    int? AssignmentVarianceMinutes, bool CanClockIn, string? BlockReason);

public sealed record CashierCashAdjustmentResponse(
    Guid Id, Guid ShiftSessionId, ApiCashAdjustmentType Type, decimal Amount, ApiCashAdjustmentStatus Status,
    string Note, string? Reference, Guid RequestedByUserId, string RequestedByName, DateTime RequestedAtUtc,
    Guid? ReviewedByUserId, string? ReviewedByName, DateTime? ReviewedAtUtc, string? ReviewNote)
{
    public string AmountDisplay => $"₱{Amount:N2}";
    public string RequestedAtDisplay => StoreDateTime.FormatUtc(RequestedAtUtc);
}

public sealed record CashierShiftCorrectionResponse(
    Guid Id, Guid ShiftSessionId, string FieldName, string? OldValue, string? NewValue, string Reason,
    Guid CorrectedByUserId, string CorrectedByName, DateTime CorrectedAtUtc)
{
    public string CorrectedAtDisplay => StoreDateTime.FormatUtc(CorrectedAtUtc);
}

public sealed record CashierShiftSaleResponse(Guid Id, string SaleNumber, ApiPaymentMethod PaymentMethod, decimal Total, DateTime SoldAtUtc)
{
    public string TotalDisplay => $"₱{Total:N2}";
    public string SoldAtDisplay => StoreDateTime.FormatUtc(SoldAtUtc);
}

public sealed record CashierShiftSessionDetailResponse(
    CashierShiftSessionResponse Session, IReadOnlyList<CashierShiftSaleResponse> Sales,
    IReadOnlyList<CashierCashAdjustmentResponse> CashAdjustments,
    IReadOnlyList<CashierShiftCorrectionResponse> Corrections);

public sealed record AdminNotificationResponse(
    Guid Id, string Type, string Title, string Message, Guid? ShiftSessionId, DateTime CreatedAtUtc, bool IsRead)
{
    public string CreatedAtDisplay => StoreDateTime.FormatUtc(CreatedAtUtc);
    public string Status => IsRead ? "Read" : "Unread";
}

public sealed record AdminNotificationPageResponse(
    IReadOnlyList<AdminNotificationResponse> Items, int Page, int PageSize, int TotalCount, int UnreadCount);

public sealed record CashierShiftReportRowResponse(
    Guid SessionId, Guid CashierUserId, string CashierName, Guid ShiftDefinitionId, string ShiftName,
    DateOnly BusinessDate, ApiCashierShiftSessionStatus Status, ApiCashierShiftCloseType? CloseType,
    DateTime ScheduledStartAtUtc, DateTime ScheduledEndAtUtc, DateTime ClockedInAtUtc,
    DateTime? ClockedOutAtUtc, int? WorkedMinutes, int ClockInVarianceMinutes, int? ClockOutVarianceMinutes,
    decimal OpeningCashFloat, decimal? TotalSales, decimal? CashSales, decimal? GCashSales,
    decimal? CashRefunds, decimal? CashPayouts, int? TransactionCount, decimal? ExpectedRemittance,
    decimal? ActualRemittance, decimal? Variance, bool? CashFloatReturned);

public sealed record CashierShiftReportSummaryResponse(
    int Sessions, decimal TotalSales, decimal CashSales, decimal GCashSales,
    decimal ExpectedRemittance, decimal ActualRemittance, decimal Variance);
public sealed record CashierShiftReportResponse(CashierShiftReportSummaryResponse Summary, IReadOnlyList<CashierShiftReportRowResponse> Sessions);

public sealed record SalesAccountabilityCategoryCellResponse(
    DateOnly BusinessDate, string Category, decimal RegularSales, decimal EmployeeSales);
public sealed record SalesAccountabilityDayResponse(
    DateOnly BusinessDate, decimal TotalSales, decimal CashSales, decimal GCashPayments,
    decimal EmployeeOwedSales, decimal ApprovedExpenses, decimal? CashRemitted,
    decimal? ExpectedCash, decimal? ActualCash, decimal? Variance,
    IReadOnlyList<string> AssignedCashiers)
{
    public string DateDisplay => BusinessDate.ToString("MMM d, yyyy");
    public string CashiersDisplay => AssignedCashiers.Count == 0 ? "-" : string.Join(" / ", AssignedCashiers);
}
public sealed record SalesAccountabilityReportResponse(
    DateOnly FromDate, DateOnly ToDateExclusive, IReadOnlyList<DateOnly> Dates,
    IReadOnlyList<string> Categories, IReadOnlyList<SalesAccountabilityCategoryCellResponse> Sales,
    IReadOnlyList<SalesAccountabilityDayResponse> Days, bool IncludesCashAccountability,
    int OtherAssignedProductCount);

public static class CashierShiftFormatting
{
    private static readonly string[] Days = ["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"];
    public static string FormatDays(byte mask) => mask == 127 ? "Every day" : string.Join(", ", Days.Where((_, index) => (mask & (1 << index)) != 0));
    public static string Money(decimal? value) => value is null ? "-" : $"₱{value:N2}";
    public static string SignedMoney(decimal? value) => value is null ? "-" : value > 0 ? $"+₱{value:N2}" : value < 0 ? $"-₱{Math.Abs(value.Value):N2}" : "₱0.00";
}

public sealed record ApiProblemDetails(
    string? Type,
    string? Title,
    int? Status,
    string? Detail,
    string? Instance,
    IReadOnlyDictionary<string, string[]>? Errors);
