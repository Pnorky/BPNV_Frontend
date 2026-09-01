using ClosedXML.Excel;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace AvaloniaApp.Services;

public static class ReportExportService
{
    public static void ExportPdf(ApiReportSnapshot report, Stream output)
    {
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
                    header.Item().Text($"Generated {StoreDateTime.FormatUtc(StoreDateTime.UtcNow)}")
                        .FontSize(8).FontColor(Colors.Grey.Darken1);
                });

                page.Content().PaddingVertical(18).Column(content =>
                {
                    content.Spacing(16);
                    content.Item().Row(row =>
                    {
                        SummaryCard(row.RelativeItem(), "Gross sales", report.Sales.Summary.GrossSales.ToString("₱#,##0.00"));
                        row.Spacing(8);
                        SummaryCard(row.RelativeItem(), "Transactions", report.Sales.Summary.Transactions.ToString());
                        row.Spacing(8);
                        SummaryCard(row.RelativeItem(), "Units sold", report.Sales.Summary.UnitsSold.ToString());
                        row.Spacing(8);
                        SummaryCard(row.RelativeItem(), "Low stock", report.Inventory.Summary.LowStockItems.ToString());
                    });

                    content.Item().Text("Sales lines").Bold().FontSize(13);
                    content.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(52);
                            columns.ConstantColumn(76);
                            columns.ConstantColumn(42);
                            columns.ConstantColumn(48);
                            columns.RelativeColumn();
                            columns.ConstantColumn(38);
                            columns.ConstantColumn(58);
                        });
                        table.Header(header =>
                        {
                            PdfHeader(header.Cell(), "Sale");
                            PdfHeader(header.Cell(), "Date and time");
                            PdfHeader(header.Cell(), "Payment");
                            PdfHeader(header.Cell(), "SKU");
                            PdfHeader(header.Cell(), "Product / unit");
                            PdfHeader(header.Cell(), "Qty");
                            PdfHeader(header.Cell(), "Amount");
                        });

                        foreach (var sale in report.Sales.Sales.OrderByDescending(sale => sale.SoldAtUtc))
                        {
                            foreach (var line in sale.Lines)
                            {
                                PdfCell(table.Cell(), sale.SaleNumber);
                                PdfCell(table.Cell(), StoreDateTime.FormatUtc(sale.SoldAtUtc));
                                PdfCell(table.Cell(), sale.PaymentMethodDisplay);
                                PdfCell(table.Cell(), line.Sku);
                                PdfCell(table.Cell(), $"{line.ProductName} ({line.UnitLabel})");
                                PdfCell(table.Cell(), line.BasePieceQuantity.ToString());
                                PdfCell(table.Cell(), line.LineTotal.ToString("₱#,##0.00"));
                            }
                        }
                    });

                    content.Item().Text("Inventory by location").Bold().FontSize(13);
                    content.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(48);
                            columns.RelativeColumn();
                            columns.ConstantColumn(58);
                            columns.ConstantColumn(42);
                            columns.ConstantColumn(42);
                            columns.ConstantColumn(38);
                            columns.ConstantColumn(58);
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

                        foreach (var product in report.Inventory.Products.OrderBy(product => product.Name))
                        {
                            PdfCell(table.Cell(), product.Sku);
                            PdfCell(table.Cell(), product.Name);
                            PdfCell(table.Cell(), product.SupplierName);
                            PdfCell(table.Cell(), product.DisplayStock.ToString());
                            PdfCell(table.Cell(), product.BodegaStock.ToString());
                            PdfCell(table.Cell(), product.TotalStock.ToString());
                            PdfCell(table.Cell(), product.StockStatus);
                        }
                    });

                    if (report.EmployeePurchases is { } employeePurchases)
                    {
                        content.Item().Text($"Employee purchases · Total deductions {employeePurchases.Summary.TotalDeductions:₱#,##0.00}").Bold().FontSize(13);
                        content.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(52); columns.ConstantColumn(65); columns.RelativeColumn();
                                columns.ConstantColumn(46); columns.RelativeColumn(); columns.ConstantColumn(34); columns.ConstantColumn(58);
                            });
                            table.Header(header =>
                            {
                                PdfHeader(header.Cell(), "Sale"); PdfHeader(header.Cell(), "Date"); PdfHeader(header.Cell(), "Employee");
                                PdfHeader(header.Cell(), "SKU"); PdfHeader(header.Cell(), "Product / unit"); PdfHeader(header.Cell(), "Qty"); PdfHeader(header.Cell(), "Amount");
                            });
                            foreach (var line in employeePurchases.Lines.OrderByDescending(line => line.SoldAtUtc))
                            {
                                PdfCell(table.Cell(), line.SaleNumber); PdfCell(table.Cell(), StoreDateTime.FormatUtc(line.SoldAtUtc));
                                PdfCell(table.Cell(), line.EmployeeDisplay); PdfCell(table.Cell(), line.Sku);
                                PdfCell(table.Cell(), $"{line.ProductName} ({line.UnitLabel})"); PdfCell(table.Cell(), line.Count.ToString());
                                PdfCell(table.Cell(), line.LineTotal.ToString("₱#,##0.00"));
                            }
                        });
                    }

                    content.Item().Text("Suggested orders").Bold().FontSize(13);
                    content.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(70);
                            columns.ConstantColumn(52);
                            columns.RelativeColumn();
                            columns.ConstantColumn(42);
                            columns.ConstantColumn(52);
                            columns.ConstantColumn(48);
                        });
                        table.Header(header =>
                        {
                            PdfHeader(header.Cell(), "Supplier");
                            PdfHeader(header.Cell(), "SKU");
                            PdfHeader(header.Cell(), "Product");
                            PdfHeader(header.Cell(), "On hand");
                            PdfHeader(header.Cell(), "Tier");
                            PdfHeader(header.Cell(), "Order");
                        });

                        foreach (var supplier in report.Orders.Suppliers)
                        {
                            foreach (var product in supplier.Products)
                            {
                                PdfCell(table.Cell(), supplier.SupplierName);
                                PdfCell(table.Cell(), product.Sku);
                                PdfCell(table.Cell(), product.ProductName);
                                PdfCell(table.Cell(), product.TotalStock.ToString());
                                PdfCell(table.Cell(), product.ReorderTier);
                                PdfCell(table.Cell(), product.SuggestedOrderQuantity.ToString());
                            }
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

    public static void ExportExcel(ApiReportSnapshot report, Stream output)
    {
        using var workbook = new XLWorkbook();
        CreateSummarySheet(workbook, report);
        CreateSalesSheet(workbook, report.Sales);
        CreateInventorySheet(workbook, report.Inventory);
        CreateOrdersSheet(workbook, report.Orders);
        if (report.EmployeePurchases is not null) CreateEmployeePurchasesSheet(workbook, report.EmployeePurchases);
        workbook.SaveAs(output);
    }

    private static void CreateSummarySheet(XLWorkbook workbook, ApiReportSnapshot report)
    {
        var sheet = workbook.Worksheets.Add("Summary");
        sheet.Cell("A1").Value = "BPNV CONVENIENCE STORE SALES AND INVENTORY REPORT";
        sheet.Range("A1:B1").Merge().Style.Font.SetBold().Font.SetFontSize(16);
        sheet.Cell("A2").Value = "Generated";
        sheet.Cell("B2").Value = StoreDateTime.StoreNow;
        sheet.Cell("B2").Style.DateFormat.Format = StoreDateTime.ExcelTimestampFormat;

        var rows = new (string Label, object Value)[]
        {
            ("Gross sales", report.Sales.Summary.GrossSales),
            ("Sales today", report.Sales.Summary.TodaySales),
            ("Transactions", report.Sales.Summary.Transactions),
            ("Units sold", report.Sales.Summary.UnitsSold),
            ("Display units", report.Inventory.Summary.DisplayUnits),
            ("Bodega units", report.Inventory.Summary.BodegaUnits),
            ("Low-stock products", report.Inventory.Summary.LowStockItems),
            ("Inventory value", report.Inventory.Summary.InventoryValue),
            ("Suppliers to order", report.Orders.Summary.SuppliersToOrder),
            ("Suggested order units", report.Orders.Summary.SuggestedOrderUnits)
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
        sheet.Range("A4:A13").Style.Font.Bold = true;
        sheet.Range("A4:B13").Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        sheet.Range("A4:B13").Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        sheet.SheetView.FreezeRows(3);
        sheet.Columns().AdjustToContents(12, 36);
    }

    private static void CreateSalesSheet(XLWorkbook workbook, SalesReportResponse sales)
    {
        var sheet = workbook.Worksheets.Add("Sales");
        string[] headers = ["Sale #", "Date & time", "Customer pricing", "Payment method", "Cashier", "SKU", "Product", "Unit", "Unit count", "Base pieces", "Unit price", "Amount"];
        WriteHeaders(sheet, headers);

        var row = 2;
        foreach (var sale in sales.Sales.OrderByDescending(sale => sale.SoldAtUtc))
        {
            foreach (var line in sale.Lines)
            {
                sheet.Cell(row, 1).Value = sale.SaleNumber;
                sheet.Cell(row, 2).Value = StoreDateTime.ToStoreTimeFromUtc(sale.SoldAtUtc);
                sheet.Cell(row, 3).Value = sale.CustomerType.ToString();
                sheet.Cell(row, 4).Value = sale.PaymentMethodDisplay;
                sheet.Cell(row, 5).Value = sale.SoldByName;
                sheet.Cell(row, 6).Value = line.Sku;
                sheet.Cell(row, 7).Value = line.ProductName;
                sheet.Cell(row, 8).Value = line.UnitLabel;
                sheet.Cell(row, 9).Value = line.Count;
                sheet.Cell(row, 10).Value = line.BasePieceQuantity;
                sheet.Cell(row, 11).Value = line.UnitPrice;
                sheet.Cell(row, 12).Value = line.LineTotal;
                row++;
            }
        }

        sheet.Column(2).Style.DateFormat.Format = StoreDateTime.ExcelTimestampFormat;
        sheet.Columns(11, 12).Style.NumberFormat.Format = "₱#,##0.00";
        StyleDataSheet(sheet, headers.Length, row - 1);
    }

    private static void CreateInventorySheet(XLWorkbook workbook, InventoryReportResponse inventory)
    {
        var sheet = workbook.Worksheets.Add("Inventory");
        string[] headers = ["SKU", "Product", "Supplier", "Type", "Category", "Unit", "Display", "Bodega", "Total", "Critical level", "Critical order", "Warning level", "Warning order", "Reorder tier", "Suggested order", "Purchase price", "Selling price", "Employee price", "Status"];
        WriteHeaders(sheet, headers);

        var row = 2;
        foreach (var product in inventory.Products.OrderBy(product => product.Name))
        {
            sheet.Cell(row, 1).Value = product.Sku;
            sheet.Cell(row, 2).Value = product.Name;
            sheet.Cell(row, 3).Value = product.SupplierName;
            sheet.Cell(row, 4).Value = product.ItemType.ToString();
            sheet.Cell(row, 5).Value = product.Category;
            sheet.Cell(row, 6).Value = product.Unit;
            sheet.Cell(row, 7).Value = product.DisplayStock;
            sheet.Cell(row, 8).Value = product.BodegaStock;
            sheet.Cell(row, 9).Value = product.TotalStock;
            sheet.Cell(row, 10).Value = product.CriticalReorderLevel;
            sheet.Cell(row, 11).Value = product.CriticalOrderQuantity;
            sheet.Cell(row, 12).Value = product.WarningReorderLevel;
            sheet.Cell(row, 13).Value = product.WarningOrderQuantity;
            sheet.Cell(row, 14).Value = product.ReorderTier;
            sheet.Cell(row, 15).Value = product.SuggestedOrderQuantity;
            sheet.Cell(row, 16).Value = product.CostPrice;
            sheet.Cell(row, 17).Value = product.RegularPrice;
            sheet.Cell(row, 18).Value = product.EmployeePrice;
            sheet.Cell(row, 19).Value = product.StockStatus;
            row++;
        }

        sheet.Columns(16, 18).Style.NumberFormat.Format = "₱#,##0.00";
        StyleDataSheet(sheet, headers.Length, row - 1);
    }

    private static void CreateOrdersSheet(XLWorkbook workbook, OrderReportResponse orders)
    {
        var sheet = workbook.Worksheets.Add("Orders");
        string[] headers = ["Supplier", "SKU", "Product", "Display", "Bodega", "Total", "Critical level", "Warning level", "Reorder tier", "Suggested order"];
        WriteHeaders(sheet, headers);
        var row = 2;
        foreach (var supplier in orders.Suppliers)
        {
            foreach (var product in supplier.Products)
            {
                sheet.Cell(row, 1).Value = supplier.SupplierName;
                sheet.Cell(row, 2).Value = product.Sku;
                sheet.Cell(row, 3).Value = product.ProductName;
                sheet.Cell(row, 4).Value = product.DisplayStock;
                sheet.Cell(row, 5).Value = product.BodegaStock;
                sheet.Cell(row, 6).Value = product.TotalStock;
                sheet.Cell(row, 7).Value = product.CriticalReorderLevel;
                sheet.Cell(row, 8).Value = product.WarningReorderLevel;
                sheet.Cell(row, 9).Value = product.ReorderTier;
                sheet.Cell(row, 10).Value = product.SuggestedOrderQuantity;
                row++;
            }
        }
        StyleDataSheet(sheet, headers.Length, row - 1);
    }

    private static void CreateEmployeePurchasesSheet(XLWorkbook workbook, EmployeePurchaseReportResponse report)
    {
        var sheet = workbook.Worksheets.Add("Employee Purchases");
        string[] headers = ["Sale #", "Date & time", "Employee ID", "Employee", "SKU", "Product", "Unit", "Unit count", "Base pieces", "Unit price", "Line total", "Sale total"];
        WriteHeaders(sheet, headers);
        var row = 2;
        foreach (var line in report.Lines.OrderByDescending(line => line.SoldAtUtc))
        {
            sheet.Cell(row, 1).Value = line.SaleNumber;
            sheet.Cell(row, 2).Value = StoreDateTime.ToStoreTimeFromUtc(line.SoldAtUtc);
            sheet.Cell(row, 3).Value = line.EmployeeNumber ?? "Unattributed";
            sheet.Cell(row, 4).Value = line.EmployeeName ?? "Unattributed";
            sheet.Cell(row, 5).Value = line.Sku;
            sheet.Cell(row, 6).Value = line.ProductName;
            sheet.Cell(row, 7).Value = line.UnitLabel;
            sheet.Cell(row, 8).Value = line.Count;
            sheet.Cell(row, 9).Value = line.BasePieceQuantity;
            sheet.Cell(row, 10).Value = line.UnitPrice;
            sheet.Cell(row, 11).Value = line.LineTotal;
            sheet.Cell(row, 12).Value = line.SaleTotal;
            row++;
        }
        sheet.Column(2).Style.DateFormat.Format = StoreDateTime.ExcelTimestampFormat;
        sheet.Columns(10, 12).Style.NumberFormat.Format = "₱#,##0.00";
        StyleDataSheet(sheet, headers.Length, row - 1);
        sheet.Cell(row + 1, 11).Value = "Total deductions";
        sheet.Cell(row + 1, 12).Value = report.Summary.TotalDeductions;
        sheet.Cell(row + 1, 12).Style.NumberFormat.Format = "₱#,##0.00";
    }

    private static void WriteHeaders(IXLWorksheet sheet, IReadOnlyList<string> headers)
    {
        for (var index = 0; index < headers.Count; index++)
            sheet.Cell(1, index + 1).Value = headers[index];
    }

    private static void StyleDataSheet(IXLWorksheet sheet, int columns, int lastRow)
    {
        var header = sheet.Range(1, 1, 1, columns);
        header.Style.Font.Bold = true;
        header.Style.Fill.BackgroundColor = XLColor.FromHtml("#F59E0B");
        header.Style.Font.FontColor = XLColor.Black;
        if (lastRow > 1)
            sheet.Range(1, 1, lastRow, columns).CreateTable();
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
