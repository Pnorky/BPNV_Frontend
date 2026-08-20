using AvaloniaApp.Services;
using ClosedXML.Excel;

namespace AvaloniaApp.Tests;

[TestClass]
public sealed class ExcelInventoryImportServiceTests
{
    [TestMethod]
    public void LegacyWorkbookIsDetectedByAnchorsAndRecalculatesBothLayouts()
    {
        using var stream = LegacyWorkbook();

        var result = new ExcelInventoryImportService().Parse(stream);

        Assert.AreEqual(ExcelInventoryWorkbookFormat.Legacy, result.Format);
        Assert.HasCount(2, result.Products);
        var upper = result.Products[0];
        Assert.AreEqual("UPPER ITEM", upper.Name);
        Assert.AreEqual(6, upper.OpeningDisplayStock);
        Assert.AreEqual(22, upper.OpeningBodegaStock);
        Assert.AreEqual(0, upper.CriticalReorderLevel);
        Assert.AreEqual(8, upper.WarningReorderLevel);
        Assert.AreEqual("VENDOR HEADING", upper.Section!.Heading);
        Assert.AreEqual("VENDOR HEADING", upper.Section.SuggestedSupplierName);
        Assert.AreEqual("", upper.SupplierName);

        var lower = result.Products[1];
        Assert.AreEqual(-2, lower.OpeningDisplayStock);
        Assert.AreEqual(8, lower.OpeningBodegaStock);
        Assert.AreEqual(12, lower.SourceRow);
        Assert.IsTrue(lower.Issues.Any(issue => issue.Code == "NegativeDisplayStock" && issue.Severity == ExcelInventoryIssueSeverity.Error));
        Assert.IsTrue(lower.Issues.Any(issue => issue.Code == "MissingReorderPoint"));
        Assert.AreEqual(ApiInventoryItemType.Merchandise, lower.Section!.SuggestedItemType);
        Assert.AreEqual(ApiInventoryItemType.Merchandise, lower.ItemType);
    }

    [TestMethod]
    public void LegacyLowerRowsSeparateSellableItemsFromInternalConsumables()
    {
        using var source = LegacyWorkbook();
        using var modified = new MemoryStream();
        using (var workbook = new XLWorkbook(source))
        {
            var sheet = workbook.Worksheet(1);
            WriteRow(sheet, 13, "JAPANESE SIOMAI 1X30", 0, 0, 0, 0, 0, 0, "", 30, 0, 0, "", 30, 10);
            WriteRow(sheet, 14, "COFFEE FILTERS 1X70", 0, 0, 0, 0, 0, 0, "", 33, 0, 0, "", 33, 10);
            workbook.SaveAs(modified);
        }
        modified.Position = 0;

        var result = new ExcelInventoryImportService().Parse(modified);

        Assert.AreEqual(ApiInventoryItemType.Merchandise, result.Products.Single(product => product.SourceRow == 13).ItemType);
        Assert.AreEqual(ApiInventoryItemType.Consumable, result.Products.Single(product => product.SourceRow == 14).ItemType);
        Assert.AreNotSame(
            result.Products.Single(product => product.SourceRow == 13).Section,
            result.Products.Single(product => product.SourceRow == 14).Section);
    }

    [TestMethod]
    public void LegacyParserIgnoresCachedEndingTotalAndStatusCells()
    {
        using var stream = LegacyWorkbook();
        using var modifiedStream = new MemoryStream();
        using (var workbook = new XLWorkbook(stream))
        {
            var sheet = workbook.Worksheet(1);
            sheet.Cell("G7").Value = 9999;
            sheet.Cell("K7").Value = 8888;
            sheet.Cell("M7").Value = 7777;
            sheet.Cell("O7").Value = "Instock";
            workbook.SaveAs(modifiedStream);
        }
        modifiedStream.Position = 0;

        var product = new ExcelInventoryImportService().Parse(modifiedStream).Products[0];

        Assert.AreEqual(6, product.OpeningDisplayStock);
        Assert.AreEqual(22, product.OpeningBodegaStock);
    }

    [TestMethod]
    public void StandardTemplateParsesAllFieldsPackagesAndLeadingZeroBarcode()
    {
        using var workbook = new XLWorkbook();
        var suppliers = workbook.Worksheets.Add("Suppliers");
        WriteRow(suppliers, 1, "Name", "ContactPerson", "Phone");
        WriteRow(suppliers, 2, "Supplier", "Person", "0917");
        var products = workbook.Worksheets.Add("Products");
        WriteRow(products, 1, "SupplierName", "ItemType", "SKU", "PieceBarcode", "Name", "Category", "Unit",
            "CostPrice", "RegularPrice", "EmployeePrice", "CriticalReorderLevel", "CriticalOrderQuantity",
            "WarningReorderLevel", "WarningOrderQuantity", "OpeningDisplayStock", "OpeningBodegaStock");
        WriteRow(products, 2, "Supplier", "Merchandise", "SKU-1", "0000123", "Product", "Snacks", "piece",
            4.5m, 6m, 5m, 0, 10, 5, 6, 3, 12);
        var packages = workbook.Worksheets.Add("Packages");
        WriteRow(packages, 1, "ProductSKU", "Barcode", "Label", "PiecesPerUnit", "RegularPrice", "EmployeePrice", "IsActive");
        WriteRow(packages, 2, "SKU-1", "0000456", "6-pack", 6, 34m, 30m, true);
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;

        var result = new ExcelInventoryImportService().Parse(stream);

        Assert.AreEqual(ExcelInventoryWorkbookFormat.StandardTemplate, result.Format);
        Assert.AreEqual("0000123", result.Products.Single().PieceBarcode);
        Assert.AreEqual(12, result.Products.Single().OpeningBodegaStock);
        Assert.AreEqual("0000456", result.Packages.Single().Barcode);
        Assert.AreEqual(6, result.Packages.Single().PiecesPerUnit);
        Assert.AreEqual("Supplier", result.Suppliers.Single().Name);
    }

    [TestMethod]
    public void GeneratedTemplateHasInstructionsAndCanBeParsedWhenEmpty()
    {
        using var stream = new MemoryStream();
        var service = new ExcelInventoryImportService();

        service.WriteTemplate(stream);
        stream.Position = 0;
        using (var workbook = new XLWorkbook(stream))
        {
            Assert.IsTrue(workbook.TryGetWorksheet("Instructions", out _));
            Assert.IsTrue(workbook.TryGetWorksheet("Suppliers", out _));
            Assert.IsTrue(workbook.TryGetWorksheet("Products", out _));
            Assert.IsTrue(workbook.TryGetWorksheet("Packages", out _));
        }
        stream.Position = 0;

        var result = service.Parse(stream);

        Assert.IsEmpty(result.Products);
        Assert.IsEmpty(result.Suppliers);
        Assert.IsEmpty(result.Packages);
    }

    private static MemoryStream LegacyWorkbook()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Not named after the file");
        WriteRow(sheet, 4, "", "DISPLAY BEGINNING STOCK", "", "", "", "", "DISPLAY ENDING STOCK", "",
            "BODEGA BEGINNING STOCK", "", "BODEGA ENDING STOCK", "", "REMAINING BALANCE", "Reorder Point", "STATUS");
        WriteRow(sheet, 5, "DATE", "", "ADD", "SALES", "AR", "SPOILAGE/BO", "", "", "", "IN", "OUT");
        sheet.Cell("A6").Value = "VENDOR HEADING";
        WriteRow(sheet, 7, "UPPER ITEM", 10, 3, 2, 1, 4, "stale", "", 20, 5, "stale", "", "stale", 8, "stale");
        sheet.Cell("A9").Value = "SALES";
        sheet.Cell("I9").Value = "CONSUMABLES";
        sheet.Cell("B10").Value = "BEGINNING";
        sheet.Cell("I10").Value = "BEGINNING";
        sheet.Cell("B11").Value = "BALANCE";
        sheet.Cell("I11").Value = "BALANCE";
        WriteRow(sheet, 12, "LOWER ITEM", 0, 1, 3, 0, 0, 999, "", 10, 2, 4, "", 999, "", "Re-order");
        var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;
        return stream;
    }

    private static void WriteRow(IXLWorksheet sheet, int row, params object[] values)
    {
        for (var index = 0; index < values.Length; index++)
        {
            var value = values[index];
            var cell = sheet.Cell(row, index + 1);
            switch (value)
            {
                case string text: cell.Value = text; break;
                case int number: cell.Value = number; break;
                case decimal number: cell.Value = number; break;
                case bool boolean: cell.Value = boolean; break;
            }
        }
    }
}
