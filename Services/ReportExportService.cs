using ClosedXML.Excel;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace AvaloniaApp.Services;

public static class ReportExportService
{
    public static void ExportPdf(StoreState store, Stream output)
    {
        var sales = store.Sales.OrderByDescending(sale => sale.SoldAt).ToList();
        var inventory = store.Products.OrderBy(product => product.Name).ToList();

        Document.Create(document =>
        {
            document.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(32);
                page.DefaultTextStyle(style => style.FontSize(9));

                page.Header().Column(header =>
                {
                    header.Item().Text("BPNV CONVENIENCE STORE").Bold().FontSize(20).FontColor(Colors.Orange.Darken2);
                    header.Item().Text("Sales and Inventory Report").SemiBold().FontSize(12);
                    header.Item().Text($"Generated {DateTime.Now:MMMM d, yyyy h:mm tt}")
                        .FontSize(8).FontColor(Colors.Grey.Darken1);
                });

                page.Content().PaddingVertical(18).Column(content =>
                {
                    content.Spacing(16);
                    content.Item().Row(row =>
                    {
                        SummaryCard(row.RelativeItem(), "Gross sales", sales.Sum(sale => sale.Total).ToString("₱#,##0.00"));
                        row.Spacing(8);
                        SummaryCard(row.RelativeItem(), "Transactions", sales.Count.ToString());
                        row.Spacing(8);
                        SummaryCard(row.RelativeItem(), "Units sold", sales.Sum(sale => sale.ItemCount).ToString());
                        row.Spacing(8);
                        SummaryCard(row.RelativeItem(), "Low stock", inventory.Count(product => product.IsLowStock).ToString());
                    });

                    content.Item().Text("Sales summary").Bold().FontSize(13);
                    content.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(58);
                            columns.RelativeColumn();
                            columns.ConstantColumn(70);
                            columns.ConstantColumn(58);
                            columns.ConstantColumn(72);
                            columns.ConstantColumn(68);
                        });
                        table.Header(header =>
                        {
                            PdfHeader(header.Cell(), "Sale");
                            PdfHeader(header.Cell(), "Date and time");
                            PdfHeader(header.Cell(), "Pricing");
                            PdfHeader(header.Cell(), "Items");
                            PdfHeader(header.Cell(), "Total");
                        });

                        foreach (var sale in sales)
                        {
                            PdfCell(table.Cell(), sale.SaleNumber);
                            PdfCell(table.Cell(), sale.SoldAt.ToString("MMM d, yyyy h:mm tt"));
                            PdfCell(table.Cell(), sale.CustomerType);
                            PdfCell(table.Cell(), sale.ItemCount.ToString());
                            PdfCell(table.Cell(), sale.Total.ToString("₱#,##0.00"));
                        }
                    });

                    content.Item().Text("Inventory by location").Bold().FontSize(13);
                    content.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(58);
                            columns.RelativeColumn();
                            columns.ConstantColumn(58);
                            columns.ConstantColumn(58);
                            columns.ConstantColumn(58);
                            columns.ConstantColumn(66);
                        });
                        table.Header(header =>
                        {
                            PdfHeader(header.Cell(), "SKU");
                            PdfHeader(header.Cell(), "Product");
                            PdfHeader(header.Cell(), "Supplier");
                            PdfHeader(header.Cell(), "Display");
                            PdfHeader(header.Cell(), "Bodega");
                            PdfHeader(header.Cell(), "Total");
                            PdfHeader(header.Cell(), "Status");
                        });

                        foreach (var product in inventory)
                        {
                            PdfCell(table.Cell(), product.Sku);
                            PdfCell(table.Cell(), product.Name);
                            PdfCell(table.Cell(), product.SupplierName);
                            PdfCell(table.Cell(), product.ShelfStock.ToString());
                            PdfCell(table.Cell(), product.BodegaStock.ToString());
                            PdfCell(table.Cell(), product.TotalStock.ToString());
                            PdfCell(table.Cell(), product.StockStatus);
                        }
                    });
                });

                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span("Page ");
                    text.CurrentPageNumber();
                    text.Span(" of ");
                    text.TotalPages();
                });
            });
        }).GeneratePdf(output);
    }

    public static void ExportExcel(StoreState store, Stream output)
    {
        using var workbook = new XLWorkbook();
        CreateSummarySheet(workbook, store);
        CreateSalesSheet(workbook, store);
        CreateInventorySheet(workbook, store);
        CreateMovementSheet(workbook, store);
        workbook.SaveAs(output);
    }

    private static void CreateSummarySheet(XLWorkbook workbook, StoreState store)
    {
        var sheet = workbook.Worksheets.Add("Summary");
        sheet.Cell("A1").Value = "BPNV CONVENIENCE STORE SALES AND INVENTORY REPORT";
        sheet.Range("A1:B1").Merge().Style.Font.SetBold().Font.SetFontSize(16);
        sheet.Cell("A2").Value = "Generated";
        sheet.Cell("B2").Value = DateTime.Now;
        sheet.Cell("B2").Style.DateFormat.Format = "mmmm d, yyyy h:mm AM/PM";

        var rows = new (string Label, object Value)[]
        {
            ("Gross sales", store.Sales.Sum(sale => sale.Total)),
            ("Sales today", store.Sales.Where(sale => sale.SoldAt.Date == DateTime.Today).Sum(sale => sale.Total)),
            ("Transactions", store.Sales.Count),
            ("Units sold", store.Sales.Sum(sale => sale.ItemCount)),
            ("Display units", store.Products.Sum(product => product.ShelfStock)),
            ("Bodega units", store.Products.Sum(product => product.BodegaStock)),
            ("Low-stock products", store.Products.Count(product => product.IsLowStock)),
            ("Inventory value", store.Products.Sum(product => product.TotalStock * product.RegularPrice))
        };

        for (var index = 0; index < rows.Length; index++)
        {
            var row = index + 4;
            sheet.Cell(row, 1).Value = rows[index].Label;
            if (rows[index].Value is decimal decimalValue)
                sheet.Cell(row, 2).Value = decimalValue;
            else if (rows[index].Value is int integerValue)
                sheet.Cell(row, 2).Value = integerValue;
        }

        sheet.Range("B4:B5").Style.NumberFormat.Format = "₱#,##0.00";
        sheet.Cell("B11").Style.NumberFormat.Format = "₱#,##0.00";
        sheet.Range("A4:A11").Style.Font.Bold = true;
        sheet.Range("A4:B11").Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        sheet.Range("A4:B11").Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        sheet.SheetView.FreezeRows(3);
        sheet.Columns().AdjustToContents(12, 36);
    }

    private static void CreateSalesSheet(XLWorkbook workbook, StoreState store)
    {
        var sheet = workbook.Worksheets.Add("Sales");
        string[] headers = ["Sale #", "Date", "Customer pricing", "SKU", "Product", "Quantity", "Unit price", "Amount"];
        WriteHeaders(sheet, headers);

        var row = 2;
        foreach (var sale in store.Sales.OrderByDescending(sale => sale.SoldAt))
        {
            foreach (var line in sale.Lines)
            {
                sheet.Cell(row, 1).Value = sale.SaleNumber;
                sheet.Cell(row, 2).Value = sale.SoldAt;
                sheet.Cell(row, 3).Value = sale.CustomerType;
                sheet.Cell(row, 4).Value = line.Sku;
                sheet.Cell(row, 5).Value = line.ProductName;
                sheet.Cell(row, 6).Value = line.Quantity;
                sheet.Cell(row, 7).Value = line.UnitPrice;
                sheet.Cell(row, 8).Value = line.Amount;
                row++;
            }
        }

        sheet.Column(2).Style.DateFormat.Format = "yyyy-mm-dd h:mm AM/PM";
        sheet.Columns(7, 8).Style.NumberFormat.Format = "₱#,##0.00";
        StyleDataSheet(sheet, headers.Length, headers.Length, row - 1);
    }

    private static void CreateInventorySheet(XLWorkbook workbook, StoreState store)
    {
        var sheet = workbook.Worksheets.Add("Inventory");
        string[] headers = ["SKU", "Product", "Supplier", "Type", "Category", "Unit", "Display", "Bodega", "Total", "Critical level", "Warning level", "Reorder tier", "Suggested order", "Purchase price", "Selling price", "Employee price", "Status"];
        WriteHeaders(sheet, headers);

        var row = 2;
        foreach (var product in store.Products.OrderBy(product => product.Name))
        {
            sheet.Cell(row, 1).Value = product.Sku;
            sheet.Cell(row, 2).Value = product.Name;
            sheet.Cell(row, 3).Value = product.SupplierName;
            sheet.Cell(row, 4).Value = product.ItemTypeDisplay;
            sheet.Cell(row, 5).Value = product.Category;
            sheet.Cell(row, 6).Value = product.Unit;
            sheet.Cell(row, 7).Value = product.ShelfStock;
            sheet.Cell(row, 8).Value = product.BodegaStock;
            sheet.Cell(row, 9).Value = product.TotalStock;
            if (product.EffectiveCriticalReorderLevel is int criticalLevel) sheet.Cell(row, 10).Value = criticalLevel;
            if (product.EffectiveWarningReorderLevel is int warningLevel) sheet.Cell(row, 11).Value = warningLevel;
            sheet.Cell(row, 12).Value = product.ReorderTier;
            sheet.Cell(row, 13).Value = product.SuggestedOrderQuantity;
            sheet.Cell(row, 14).Value = product.CostPrice;
            sheet.Cell(row, 15).Value = product.RegularPrice;
            sheet.Cell(row, 16).Value = product.EmployeePrice;
            sheet.Cell(row, 17).Value = product.StockStatus;
            row++;
        }

        sheet.Columns(14, 16).Style.NumberFormat.Format = "₱#,##0.00";
        StyleDataSheet(sheet, headers.Length, headers.Length, row - 1);
    }

    private static void CreateMovementSheet(XLWorkbook workbook, StoreState store)
    {
        var sheet = workbook.Worksheets.Add("Stock Movements");
        string[] headers = ["Date", "SKU", "Product", "Supplier", "Movement", "Quantity", "Reference", "Notes"];
        WriteHeaders(sheet, headers);
        var row = 2;
        foreach (var movement in store.Movements.OrderByDescending(item => item.OccurredAt))
        {
            sheet.Cell(row, 1).Value = movement.OccurredAt;
            sheet.Cell(row, 2).Value = movement.Sku;
            sheet.Cell(row, 3).Value = movement.ProductName;
            sheet.Cell(row, 4).Value = movement.SupplierName;
            sheet.Cell(row, 5).Value = movement.TypeDisplay;
            sheet.Cell(row, 6).Value = movement.QuantityDisplay;
            sheet.Cell(row, 7).Value = movement.Reference;
            sheet.Cell(row, 8).Value = movement.Notes;
            row++;
        }
        sheet.Column(1).Style.DateFormat.Format = "yyyy-mm-dd h:mm AM/PM";
        StyleDataSheet(sheet, headers.Length, headers.Length, row - 1);
    }

    private static void WriteHeaders(IXLWorksheet sheet, IReadOnlyList<string> headers)
    {
        for (var index = 0; index < headers.Count; index++)
            sheet.Cell(1, index + 1).Value = headers[index];
    }

    private static void StyleDataSheet(IXLWorksheet sheet, int headerColumns, int lastColumn, int lastRow)
    {
        var header = sheet.Range(1, 1, 1, headerColumns);
        header.Style.Font.Bold = true;
        header.Style.Fill.BackgroundColor = XLColor.FromHtml("#F59E0B");
        header.Style.Font.FontColor = XLColor.Black;
        if (lastRow >= 1)
            sheet.Range(1, 1, lastRow, lastColumn).CreateTable();
        sheet.SheetView.FreezeRows(1);
        sheet.Columns().AdjustToContents(8, 36);
    }

    private static void SummaryCard(IContainer container, string label, string value) => container
        .Border(1).BorderColor(Colors.Grey.Lighten2).Padding(8).Column(column =>
        {
            column.Item().Text(label).FontSize(8).FontColor(Colors.Grey.Darken1);
            column.Item().Text(value).Bold().FontSize(12);
        });

    private static void PdfHeader(IContainer container, string text) => container
        .Background(Colors.Orange.Medium).Padding(5).Text(text).Bold().FontSize(8);

    private static void PdfCell(IContainer container, string text) => container
        .BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(text).FontSize(8);
}
