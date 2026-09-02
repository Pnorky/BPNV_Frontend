using AvaloniaApp.Services;
using ClosedXML.Excel;
using QuestPDF.Infrastructure;
using QuestSettings = QuestPDF.Settings;

namespace AvaloniaApp.Tests;

[TestClass]
public sealed class ReportExportServiceTests
{
    [TestMethod]
    public void ExcelExportUsesApiReportSnapshot()
    {
        using var output = new MemoryStream();

        ReportExportService.ExportExcel(Snapshot(), output);

        output.Position = 0;
        using var workbook = new XLWorkbook(output);
        CollectionAssert.AreEqual(
            new[] { "Summary", "Sales", "Inventory", "Orders", "Employee Purchases" },
            workbook.Worksheets.Select(sheet => sheet.Name).ToArray());
        Assert.AreEqual("SALE-1", workbook.Worksheet("Sales").Cell("A2").GetString());
        Assert.AreEqual("Cash", workbook.Worksheet("Sales").Cell("D2").GetString());
        Assert.AreEqual("SKU-1", workbook.Worksheet("Inventory").Cell("A2").GetString());
        Assert.AreEqual("Supplier A", workbook.Worksheet("Orders").Cell("A2").GetString());
        Assert.AreEqual("EMP-000001", workbook.Worksheet("Employee Purchases").Cell("C2").GetString());
        Assert.AreEqual(new DateTime(2026, 8, 27, 9, 0, 0), workbook.Worksheet("Sales").Cell("B2").GetDateTime());
        Assert.AreEqual(StoreDateTime.ExcelTimestampFormat, workbook.Worksheet("Sales").Cell("B2").Style.DateFormat.Format);
        Assert.AreEqual(new DateTime(2026, 8, 27, 9, 0, 0), workbook.Worksheet("Employee Purchases").Cell("B2").GetDateTime());
        Assert.AreEqual(StoreDateTime.ExcelTimestampFormat, workbook.Worksheet("Employee Purchases").Cell("B2").Style.DateFormat.Format);
    }

    [TestMethod]
    public void PdfExportUsesApiReportSnapshot()
    {
        QuestSettings.License = LicenseType.Community;
        using var output = new MemoryStream();

        ReportExportService.ExportPdf(Snapshot(), output);

        Assert.IsTrue(output.Length > 0);
        CollectionAssert.AreEqual("%PDF"u8.ToArray(), output.ToArray()[..4]);
    }

    private static ApiReportSnapshot Snapshot()
    {
        var productId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var supplierId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var sale = new ReportSaleResponse(
            Guid.Parse("33333333-3333-3333-3333-333333333333"),
            "SALE-1",
            ApiCustomerType.Regular,
            ApiPaymentMethod.Cash,
            25m,
            25m,
            new DateTime(2026, 8, 27, 1, 0, 0, DateTimeKind.Utc),
            Guid.Parse("44444444-4444-4444-4444-444444444444"),
            "Cashier",
            [new ReportSaleLineResponse(
                Guid.Parse("55555555-5555-5555-5555-555555555555"), productId,
                Guid.Parse("66666666-6666-6666-6666-666666666666"), "SKU-1", "Product", "piece", null,
                1, 2, 2, 12.5m, 25m)]);
        var inventoryProduct = new InventoryReportProductResponse(
            productId, supplierId, "Supplier A", ApiInventoryItemType.Merchandise, "SKU-1", "Product",
            "General", "piece", 8m, 12.5m, 10m, 2, 3, 5, 2, 10, 4, 6, "Critical", 10,
            "Low stock", true);
        var orderProduct = new OrderProductResponse(
            productId, "SKU-1", "Product", 2, 3, 5, 2, 4, "Critical", 10);

        return new ApiReportSnapshot(
            new SalesReportResponse(
                new SalesReportSummaryResponse(25m, 25m, 1, 2),
                [new TopProductResponse(productId, "SKU-1", "Product", 2, 25m)],
                [sale],
                [sale]),
            new InventoryReportResponse(
                new InventoryReportSummaryResponse(1, 62.5m, 2, 3, 5, 1, 0, 0),
                [inventoryProduct]),
            new OrderReportResponse(
                new OrderReportSummaryResponse(1, 1, 10),
                [new SupplierOrderResponse(supplierId, "Supplier A", 1, 10, [orderProduct])]),
            new EmployeePurchaseReportResponse(
                new EmployeePurchaseSummaryResponse(25m, 1, 1),
                [new EmployeePurchaseLineResponse(
                    sale.Id, sale.SaleNumber, sale.SoldAtUtc, Guid.NewGuid(), "EMP-000001", "Employee One",
                    productId, "SKU-1", "Product", "piece", 2, 2, 12.5m, 25m, 25m)]));
    }
}
