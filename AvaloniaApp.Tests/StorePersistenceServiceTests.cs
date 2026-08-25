using System.Text.Json;
using AvaloniaApp.Services;

namespace AvaloniaApp.Tests;

[TestClass]
public sealed class StorePersistenceServiceTests
{
    [TestMethod]
    public void LoadNormalizesLegacyManilaEventTimesToUtc()
    {
        var path = Path.Combine(Path.GetTempPath(), $"bpnv-time-{Guid.NewGuid():N}.json");
        try
        {
            var legacyTime = new DateTime(2025, 8, 26, 8, 0, 0, DateTimeKind.Unspecified);
            var document = new StoreDocument
            {
                Sales =
                [
                    new SaleRecord
                    {
                        SaleNumber = "S-1",
                        SoldAt = legacyTime,
                        CustomerType = "Regular",
                        Lines = []
                    }
                ],
                Movements =
                [
                    new StockMovement
                    {
                        ProductId = Guid.NewGuid(),
                        Sku = "SKU-1",
                        ProductName = "Product",
                        SupplierName = "Supplier",
                        Type = StockMovementType.Receipt,
                        Quantity = 1,
                        OccurredAt = legacyTime
                    }
                ]
            };
            File.WriteAllText(path, JsonSerializer.Serialize(document));

            var loaded = new StorePersistenceService(path).Load();

            var expected = new DateTime(2025, 8, 26, 0, 0, 0, DateTimeKind.Utc);
            Assert.AreEqual(expected, loaded.Sales.Single().SoldAt);
            Assert.AreEqual(DateTimeKind.Utc, loaded.Sales.Single().SoldAt.Kind);
            Assert.AreEqual(expected, loaded.Movements.Single().OccurredAt);
            Assert.AreEqual(DateTimeKind.Utc, loaded.Movements.Single().OccurredAt.Kind);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
