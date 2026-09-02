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

public enum ApiCustomerType
{
    Regular,
    Employee
}

public enum ApiPaymentMethod
{
    Cash,
    GCash
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
    IReadOnlyList<ProductUnitResponse> Units)
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
    public string EmployeePriceDisplay => EmployeePrice > 0 ? $"₱{EmployeePrice:N2}" : "Same as selling";
    public string StockDisplay => $"{DisplayStock} display / {BodegaStock} bodega";
    public string BarcodeDisplay => string.IsNullOrWhiteSpace(Barcode) ? "No barcode (optional)" : Barcode;
    public bool CanRecordStockCount => IsActive && ItemType != ApiInventoryItemType.Merchandise;
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
    ProductUnitResponse? SelectedUnit)
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
    IReadOnlyList<CreateProductUnitRequest>? Packages);

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
    IReadOnlyList<UpdateProductUnitRequest>? Packages);

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
    IReadOnlyList<InventoryImportPackageRequest>? Packages);

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

public sealed record ReceiveStockRequest(Guid ProductId, Guid UnitId, int Count, decimal UnitCost, decimal SellingPrice, decimal EmployeePrice, string? Reference, string? Notes);
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
    DateTime OccurredAtUtc)
{
    public string OccurredAtDisplay => StoreDateTime.FormatUtc(OccurredAtUtc);
}

public sealed record BatchReceiptRecordRequest(
    int SourceRecord,
    string SupplierLibrary,
    string Barcode,
    int UnitQuantity);

public sealed record BatchReceiptRequest(
    Guid IdempotencyKey,
    string? Reference,
    string? Notes,
    IReadOnlyList<BatchReceiptRecordRequest> Records);

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
    IReadOnlyList<BatchReceiptIssueResponse> Issues)
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
}

public sealed record BatchReceiptValidationSummaryResponse(
    int InputRecordCount,
    int NormalizedLineCount,
    int AffectedProductCount,
    int? TotalBasePieces,
    int WarningCount,
    int ErrorCount,
    int IssueCount);

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
    bool IsIdempotentReplay)
{
    public string ReferenceDisplay => string.IsNullOrWhiteSpace(Reference) ? BatchId.ToString() : Reference;
    public string SuppliersDisplay => string.Join(", ", Suppliers);
    public string CompletedAtDisplay => StoreDateTime.FormatUtc(CompletedAtUtc);
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
    string CreatedByName)
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
    private static string Signed(int value) => value > 0 ? $"+{value:N0}" : value.ToString("N0");
}

public sealed record CreateSaleLineRequest(Guid UnitId, int Count);

public sealed record CreateSaleRequest(
    Guid IdempotencyKey,
    ApiCustomerType CustomerType,
    ApiPaymentMethod PaymentMethod,
    IReadOnlyList<CreateSaleLineRequest> Lines,
    Guid? EmployeeId = null);

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
    string? EmployeeName = null)
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
    string? EmployeeName = null)
{
    public int ItemCount => Lines.Sum(line => line.BasePieceQuantity);
    public string PaymentMethodDisplay => PaymentMethod.ToString();
    public string TotalDisplay => $"₱{Total:N2}";
    public string TimeDisplay => StoreDateTime.FormatUtc(SoldAtUtc);
}

public sealed record EmployeePurchaseSummaryResponse(decimal TotalDeductions, int Transactions, int Employees);

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
    decimal SaleTotal)
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
    EmployeePurchaseReportResponse? EmployeePurchases = null);

public sealed record ApiProblemDetails(
    string? Type,
    string? Title,
    int? Status,
    string? Detail,
    string? Instance,
    IReadOnlyDictionary<string, string[]>? Errors);
