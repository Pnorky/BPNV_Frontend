namespace AvaloniaApp.Services;

public enum ApiInventoryItemType
{
    Merchandise,
    Consumable,
    Supply
}

public enum ApiCustomerType
{
    Regular,
    Employee
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
    public string StockStatus => TotalStock == 0 ? "Out of stock" : IsCriticalStock ? "Critical" : IsLowStock ? "Warning" : "In stock";
    public string ReorderActionDisplay => SuggestedOrderQuantity > 0
        ? $"Order {SuggestedOrderQuantity} pieces"
        : "No order needed";
    public string ReorderRulesDisplay => $"Critical ≤ {CriticalReorderLevel}: {CriticalOrderQuantity} · Warning ≤ {WarningReorderLevel}: {WarningOrderQuantity}";
    public string PurchasePriceDisplay => $"₱{CostPrice:N2}";
    public string SellingPriceDisplay => $"₱{RegularPrice:N2}";
    public string EmployeePriceDisplay => EmployeePrice > 0 ? $"₱{EmployeePrice:N2}" : "Same as selling";
    public string StockDisplay => $"{DisplayStock} display / {BodegaStock} bodega";
}

public sealed record SupplierResponse(
    Guid Id,
    string Name,
    string? ContactPerson,
    string? Phone,
    bool IsActive)
{
    public string Details => string.Join(" · ", new[] { ContactPerson, Phone }.Where(value => !string.IsNullOrWhiteSpace(value)));
    public override string ToString() => Name;
}

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
    string PieceBarcode,
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

public sealed record PagedResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);

public sealed record ReceiveStockRequest(Guid ProductId, Guid UnitId, int Count, string? Reference, string? Notes);

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
    DateTime OccurredAtUtc);

public sealed record CreateSaleLineRequest(Guid UnitId, int Count);

public sealed record CreateSaleRequest(
    Guid IdempotencyKey,
    ApiCustomerType CustomerType,
    IReadOnlyList<CreateSaleLineRequest> Lines);

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
    decimal Subtotal,
    decimal Total,
    DateTime SoldAtUtc,
    Guid SoldByUserId,
    bool IsIdempotentReplay,
    IReadOnlyList<SaleLineResponse> Lines);

public sealed record ApiProblemDetails(
    string? Type,
    string? Title,
    int? Status,
    string? Detail,
    string? Instance,
    IReadOnlyDictionary<string, string[]>? Errors);
