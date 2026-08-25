using System.Text.Json;

namespace AvaloniaApp.Services;

public sealed class StorePersistenceService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly string _path;

    public StorePersistenceService(string? path = null)
    {
        _path = path ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "BNPV.Stockroom",
            "store.json");
    }

    public StoreDocument Load()
    {
        if (!File.Exists(_path)) return new StoreDocument();
        try
        {
            using var stream = File.OpenRead(_path);
            return NormalizeEventTimes(JsonSerializer.Deserialize<StoreDocument>(stream, JsonOptions) ?? new StoreDocument());
        }
        catch (JsonException)
        {
            var backup = _path + ".bak";
            if (!File.Exists(backup)) return new StoreDocument();
            using var stream = File.OpenRead(backup);
            return NormalizeEventTimes(JsonSerializer.Deserialize<StoreDocument>(stream, JsonOptions) ?? new StoreDocument());
        }
    }

    public void Save(StoreState store, int nextSaleNumber)
    {
        var directory = Path.GetDirectoryName(_path)!;
        Directory.CreateDirectory(directory);
        var temporaryPath = _path + ".tmp";
        var backupPath = _path + ".bak";
        var document = new StoreDocument
        {
            NextSaleNumber = nextSaleNumber,
            Suppliers = store.Suppliers.ToList(),
            Products = store.Products.ToList(),
            Sales = store.Sales.ToList(),
            Movements = store.Movements.ToList()
        };

        using (var stream = File.Create(temporaryPath))
            JsonSerializer.Serialize(stream, document, JsonOptions);

        if (File.Exists(_path)) File.Copy(_path, backupPath, true);
        File.Move(temporaryPath, _path, true);
    }

    private static StoreDocument NormalizeEventTimes(StoreDocument document) => new()
    {
        Version = document.Version,
        NextSaleNumber = document.NextSaleNumber,
        Suppliers = document.Suppliers,
        Products = document.Products,
        Sales = document.Sales.Select(sale => new SaleRecord
        {
            SaleNumber = sale.SaleNumber,
            SoldAt = StoreDateTime.NormalizeEventToUtc(sale.SoldAt),
            CustomerType = sale.CustomerType,
            Lines = sale.Lines
        }).ToList(),
        Movements = document.Movements.Select(movement => new StockMovement
        {
            Id = movement.Id,
            ProductId = movement.ProductId,
            Sku = movement.Sku,
            ProductName = movement.ProductName,
            SupplierName = movement.SupplierName,
            Type = movement.Type,
            Quantity = movement.Quantity,
            OccurredAt = StoreDateTime.NormalizeEventToUtc(movement.OccurredAt),
            Reference = movement.Reference,
            Notes = movement.Notes
        }).ToList()
    };
}

public sealed class StoreDocument
{
    public int Version { get; init; } = 1;
    public int NextSaleNumber { get; init; } = 1;
    public List<SupplierItem> Suppliers { get; init; } = [];
    public List<ProductItem> Products { get; init; } = [];
    public List<SaleRecord> Sales { get; init; } = [];
    public List<StockMovement> Movements { get; init; } = [];
}
