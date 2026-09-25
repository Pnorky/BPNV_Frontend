using ClosedXML.Excel;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace AvaloniaApp.Services;

public enum ReportExportArea
{
    Sales = 0,
    EmployeePurchases = 1,
    CashierRemittance = 2,
    Inventory = 3,
    Orders = 4,
    SalesAccountability = 5,
    All = 6
}

public static class ReportExportService
{
    public static void ExportPdf(ApiReportSnapshot report, Stream output, ReportExportArea area = ReportExportArea.All)
    {
        if (area != ReportExportArea.All)
        {
            ExportSingleAreaPdf(report, output, area);
            return;
        }

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

                    content.Item().Text("Sales summary").Bold().FontSize(13);
                    content.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(58);
                            columns.ConstantColumn(76);
                            columns.ConstantColumn(54);
                            columns.ConstantColumn(42);
                            columns.ConstantColumn(46);
                            columns.RelativeColumn();
                            columns.ConstantColumn(38);
                            columns.ConstantColumn(52);
                            columns.ConstantColumn(58);
                        });
                        table.Header(header =>
                        {
                            PdfHeader(header.Cell(), "Sale");
                            PdfHeader(header.Cell(), "Date and time");
                            PdfHeader(header.Cell(), "Sale type");
                            PdfHeader(header.Cell(), "Payment");
                            PdfHeader(header.Cell(), "SKU");
                            PdfHeader(header.Cell(), "Product / unit");
                            PdfHeader(header.Cell(), "Price");
                            PdfHeader(header.Cell(), "Qty");
                            PdfHeader(header.Cell(), "Amount");
                        });

                        foreach (var sale in report.Sales.Sales.OrderByDescending(sale => sale.SoldAtUtc))
                        {
                            var firstLine = true;
                            foreach (var line in sale.Lines)
                            {
                                PdfCell(table.Cell(), firstLine ? sale.SaleNumber : "");
                                PdfCell(table.Cell(), firstLine ? StoreDateTime.FormatUtc(sale.SoldAtUtc) : "");
                                PdfCell(table.Cell(), firstLine ? sale.CustomerType.ToString() : "");
                                PdfCell(table.Cell(), firstLine ? sale.PaymentMethodDisplay : "");
                                PdfCell(table.Cell(), line.Sku);
                                PdfCell(table.Cell(), $"{line.ProductName} ({line.UnitLabel})");
                                PdfCell(table.Cell(), line.UnitPrice.ToString("₱#,##0.00"));
                                PdfCell(table.Cell(), line.BasePieceQuantity.ToString());
                                PdfCell(table.Cell(), line.LineTotal.ToString("₱#,##0.00"));
                                firstLine = false;
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
                        var employeePaid = employeePurchases.Summary.TotalDeductions - employeePurchases.Summary.TotalOwed;
                        content.Item().Text($"Employee purchases · Paid {employeePaid:₱#,##0.00} · Owed {employeePurchases.Summary.TotalOwed:₱#,##0.00}").Bold().FontSize(13);
                        var displayedEmployees = new HashSet<string>();
                        content.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(); columns.ConstantColumn(62); columns.ConstantColumn(68);
                                columns.ConstantColumn(48); columns.RelativeColumn(); columns.ConstantColumn(52); columns.ConstantColumn(38);
                                columns.ConstantColumn(58); columns.ConstantColumn(58);
                            });
                            table.Header(header =>
                            {
                                PdfHeader(header.Cell(), "Employee"); PdfHeader(header.Cell(), "Sale"); PdfHeader(header.Cell(), "Date & time");
                                PdfHeader(header.Cell(), "SKU"); PdfHeader(header.Cell(), "Product / unit"); PdfHeader(header.Cell(), "Price"); PdfHeader(header.Cell(), "Qty");
                                PdfHeader(header.Cell(), "Paid"); PdfHeader(header.Cell(), "Owed");
                            });
                            foreach (var employee in employeePurchases.Lines.GroupBy(line => line.EmployeeDisplay).OrderBy(group => group.Key))
                            foreach (var sale in employee.GroupBy(line => new { line.SaleId, line.SaleNumber, line.SoldAtUtc, line.EmployeeDisplay }).OrderByDescending(group => group.Key.SoldAtUtc))
                            {
                                var firstLine = true;
                                foreach (var line in sale)
                                {
                                    PdfCell(table.Cell(), firstLine && displayedEmployees.Add(line.EmployeeDisplay) ? line.EmployeeDisplay : ""); PdfCell(table.Cell(), firstLine ? line.SaleNumber : "");
                                    PdfCell(table.Cell(), firstLine ? StoreDateTime.FormatUtc(line.SoldAtUtc) : ""); PdfCell(table.Cell(), line.Sku);
                                    PdfCell(table.Cell(), $"{line.ProductName} ({line.UnitLabel})"); PdfCell(table.Cell(), line.UnitPrice.ToString("₱#,##0.00"));
                                    PdfCell(table.Cell(), line.BasePieceQuantity.ToString());
                                    if (line == sale.Last())
                                    {
                                        PdfCell(table.Cell(), (line.IsOwed ? 0m : line.SaleTotal).ToString("₱#,##0.00"));
                                        PdfCell(table.Cell(), (line.IsOwed ? line.SaleTotal : 0m).ToString("₱#,##0.00"));
                                    }
                                    else
                                    {
                                        PdfCell(table.Cell(), ""); PdfCell(table.Cell(), "");
                                    }
                                    firstLine = false;
                                }
                            }
                        });
                        content.Item().Text("Employee owed summary").Bold().FontSize(11);
                        content.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns => { columns.RelativeColumn(); columns.ConstantColumn(80); });
                            table.Header(header => { PdfHeader(header.Cell(), "Employee"); PdfHeader(header.Cell(), "Owed"); });
                            foreach (var employee in employeePurchases.Lines.GroupBy(line => line.EmployeeDisplay).OrderBy(group => group.Key))
                            {
                                PdfCell(table.Cell(), employee.Key);
                                PdfCell(table.Cell(), employee.Where(line => line.IsOwed).GroupBy(line => line.SaleId).Sum(sale => sale.First().SaleTotal).ToString("₱#,##0.00"));
                            }
                        });
                    }

                    content.Item().Text("Suggested orders").Bold().FontSize(13);
                    foreach (var supplier in report.Orders.Suppliers)
                    {
                        content.Item().Text(supplier.SupplierName).Bold().FontSize(10);
                        content.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(58);
                                columns.RelativeColumn();
                                columns.ConstantColumn(48);
                                columns.ConstantColumn(48);
                                columns.ConstantColumn(48);
                                columns.ConstantColumn(58);
                                columns.ConstantColumn(58);
                                columns.ConstantColumn(58);
                            });
                            table.Header(header =>
                            {
                                PdfHeader(header.Cell(), "SKU");
                                PdfHeader(header.Cell(), "Product");
                                PdfHeader(header.Cell(), "Bodega");
                                PdfHeader(header.Cell(), "Display");
                                PdfHeader(header.Cell(), "Total");
                                PdfHeader(header.Cell(), "Critical level");
                                PdfHeader(header.Cell(), "Warning level");
                                PdfHeader(header.Cell(), "How many to order");
                            });
                            foreach (var product in supplier.Products)
                            {
                                PdfCell(table.Cell(), product.Sku);
                                PdfCell(table.Cell(), product.ProductName);
                                PdfCell(table.Cell(), product.BodegaStock.ToString());
                                PdfCell(table.Cell(), product.DisplayStock.ToString());
                                PdfCell(table.Cell(), product.TotalStock.ToString());
                                PdfCell(table.Cell(), product.CriticalReorderLevel.ToString());
                                PdfCell(table.Cell(), product.WarningReorderLevel.ToString());
                                PdfCell(table.Cell(), product.SuggestedOrderQuantity.ToString());
                            }
                        });
                    }
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

    private static void ExportSingleAreaPdf(ApiReportSnapshot report, Stream output, ReportExportArea area)
    {
        Document.Create(document => document.Page(page =>
        {
            page.Size(area == ReportExportArea.CashierRemittance ? PageSizes.A4.Landscape() : PageSizes.A4);
            page.Margin(32);
            page.DefaultTextStyle(style => style.FontSize(9));
            page.Header().Column(header =>
            {
                header.Item().Text("BPNV CONVENIENCE STORE").Bold().FontSize(20).FontColor(Colors.Orange.Darken2);
                header.Item().Text(area switch
                {
                    ReportExportArea.EmployeePurchases => "Employee Purchases Report",
                    ReportExportArea.CashierRemittance => "Cashier Remittance Report",
                    ReportExportArea.Inventory => "Inventory Report",
                    _ => "Order Report"
                }).SemiBold().FontSize(12);
            });
            page.Content().PaddingVertical(18).Column(content =>
            {
                content.Spacing(12);
                switch (area)
                {
                    case ReportExportArea.Sales:
                        content.Item().Text("Sales summary").Bold().FontSize(13);
                        content.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns => { columns.ConstantColumn(58); columns.ConstantColumn(76); columns.ConstantColumn(54); columns.ConstantColumn(46); columns.ConstantColumn(46); columns.RelativeColumn(); columns.ConstantColumn(52); columns.ConstantColumn(38); columns.ConstantColumn(58); });
                            table.Header(header => { PdfHeader(header.Cell(), "Sale"); PdfHeader(header.Cell(), "Date and time"); PdfHeader(header.Cell(), "Sale type"); PdfHeader(header.Cell(), "Payment"); PdfHeader(header.Cell(), "SKU"); PdfHeader(header.Cell(), "Product / unit"); PdfHeader(header.Cell(), "Price"); PdfHeader(header.Cell(), "Qty"); PdfHeader(header.Cell(), "Amount"); });
                            foreach (var sale in report.Sales.Sales.OrderByDescending(sale => sale.SoldAtUtc))
                            {
                                var firstLine = true;
                                foreach (var line in sale.Lines)
                                {
                                    PdfCell(table.Cell(), firstLine ? sale.SaleNumber : ""); PdfCell(table.Cell(), firstLine ? StoreDateTime.FormatUtc(sale.SoldAtUtc) : "");
                                    PdfCell(table.Cell(), firstLine ? sale.CustomerType.ToString() : ""); PdfCell(table.Cell(), firstLine ? sale.PaymentMethodDisplay : "");
                                    PdfCell(table.Cell(), line.Sku); PdfCell(table.Cell(), $"{line.ProductName} ({line.UnitLabel})"); PdfCell(table.Cell(), line.UnitPrice.ToString("₱#,##0.00"));
                                    PdfCell(table.Cell(), line.BasePieceQuantity.ToString()); PdfCell(table.Cell(), line.LineTotal.ToString("₱#,##0.00"));
                                    firstLine = false;
                                }
                            }
                        });
                        break;
                    case ReportExportArea.EmployeePurchases when report.EmployeePurchases is { } employee:
                        var displayedEmployees = new HashSet<string>();
                        content.Item().Text($"Employee purchases · Paid {(employee.Summary.TotalDeductions - employee.Summary.TotalOwed):₱#,##0.00} · Owed {employee.Summary.TotalOwed:₱#,##0.00}").Bold().FontSize(13);
                        content.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns => { columns.RelativeColumn(); columns.ConstantColumn(62); columns.ConstantColumn(68); columns.ConstantColumn(48); columns.RelativeColumn(); columns.ConstantColumn(52); columns.ConstantColumn(38); columns.ConstantColumn(58); columns.ConstantColumn(58); });
                            table.Header(header => { PdfHeader(header.Cell(), "Employee"); PdfHeader(header.Cell(), "Sale"); PdfHeader(header.Cell(), "Date & time"); PdfHeader(header.Cell(), "SKU"); PdfHeader(header.Cell(), "Product / unit"); PdfHeader(header.Cell(), "Price"); PdfHeader(header.Cell(), "Qty"); PdfHeader(header.Cell(), "Paid"); PdfHeader(header.Cell(), "Owed"); });
                            foreach (var employeeGroup in employee.Lines.GroupBy(line => line.EmployeeDisplay).OrderBy(group => group.Key))
                            foreach (var sale in employeeGroup.GroupBy(line => new { line.SaleId, line.SaleNumber, line.SoldAtUtc, line.EmployeeDisplay }).OrderByDescending(group => group.Key.SoldAtUtc))
                            {
                                var firstLine = true;
                                foreach (var line in sale)
                                {
                                    PdfCell(table.Cell(), firstLine && displayedEmployees.Add(line.EmployeeDisplay) ? line.EmployeeDisplay : ""); PdfCell(table.Cell(), firstLine ? line.SaleNumber : ""); PdfCell(table.Cell(), firstLine ? StoreDateTime.FormatUtc(line.SoldAtUtc) : "");
                                    PdfCell(table.Cell(), line.Sku); PdfCell(table.Cell(), $"{line.ProductName} ({line.UnitLabel})"); PdfCell(table.Cell(), line.UnitPrice.ToString("₱#,##0.00")); PdfCell(table.Cell(), line.BasePieceQuantity.ToString());
                                    if (line == sale.Last()) { PdfCell(table.Cell(), (line.IsOwed ? 0m : line.SaleTotal).ToString("₱#,##0.00")); PdfCell(table.Cell(), (line.IsOwed ? line.SaleTotal : 0m).ToString("₱#,##0.00")); }
                                    else { PdfCell(table.Cell(), ""); PdfCell(table.Cell(), ""); }
                                    firstLine = false;
                                }
                            }
                        });
                        content.Item().Text("Employee owed summary").Bold().FontSize(11);
                        content.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns => { columns.RelativeColumn(); columns.ConstantColumn(80); });
                            table.Header(header => { PdfHeader(header.Cell(), "Employee"); PdfHeader(header.Cell(), "Owed"); });
                            foreach (var group in employee.Lines.GroupBy(line => line.EmployeeDisplay).OrderBy(group => group.Key)) { PdfCell(table.Cell(), group.Key); PdfCell(table.Cell(), group.Where(line => line.IsOwed).GroupBy(line => line.SaleId).Sum(sale => sale.First().SaleTotal).ToString("₱#,##0.00")); }
                        });
                        break;
                    case ReportExportArea.CashierRemittance when report.CashierShifts is { } cashier:
                        content.Item().Text("Cashier remittance summary").Bold().FontSize(13);
                        content.Item().Text($"Sessions: {cashier.Summary.Sessions}   Cash collected: {cashier.Summary.CashSales:₱#,##0.00}   Expected cash: {cashier.Summary.ExpectedRemittance:₱#,##0.00}   Actual cash: {cashier.Summary.ActualRemittance:₱#,##0.00}   Remittance difference: {cashier.Summary.Variance:₱#,##0.00}");
                        content.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(1.2f); columns.RelativeColumn(1.4f); columns.RelativeColumn(1.4f); columns.RelativeColumn(1.1f);
                                columns.RelativeColumn(1f); columns.RelativeColumn(1.1f); columns.RelativeColumn(1.1f); columns.RelativeColumn(1.1f);
                                columns.RelativeColumn(1f); columns.RelativeColumn(0.9f); columns.RelativeColumn(0.9f); columns.RelativeColumn(0.9f);
                                columns.RelativeColumn(1.3f); columns.RelativeColumn(1.3f); columns.RelativeColumn(0.9f); columns.RelativeColumn(1.1f);
                            });
                            table.Header(header => { PdfHeader(header.Cell(), "Date"); PdfHeader(header.Cell(), "Cashier"); PdfHeader(header.Cell(), "Shift"); PdfHeader(header.Cell(), "Status"); PdfHeader(header.Cell(), "Sales"); PdfHeader(header.Cell(), "Expected cash"); PdfHeader(header.Cell(), "Actual cash"); PdfHeader(header.Cell(), "Difference"); PdfHeader(header.Cell(), "Starting cash"); PdfHeader(header.Cell(), "Refunds"); PdfHeader(header.Cell(), "Payouts"); PdfHeader(header.Cell(), "Sales count"); PdfHeader(header.Cell(), "Clock in"); PdfHeader(header.Cell(), "Clock out"); PdfHeader(header.Cell(), "Length"); PdfHeader(header.Cell(), "Cash returned"); });
                            foreach (var session in cashier.Sessions.OrderByDescending(session => session.BusinessDate))
                            {
                                PdfCell(table.Cell(), session.BusinessDate.ToString("MMMM d, yyyy")); PdfCell(table.Cell(), session.CashierName); PdfCell(table.Cell(), session.ShiftName);
                                PdfCell(table.Cell(), session.Status == ApiCashierShiftSessionStatus.ClosedPendingRemittance ? "Pending" : session.Status.ToString());
                                PdfCell(table.Cell(), CashierShiftFormatting.Money(session.TotalSales)); PdfCell(table.Cell(), CashierShiftFormatting.Money(session.ExpectedRemittance)); PdfCell(table.Cell(), CashierShiftFormatting.Money(session.ActualRemittance)); PdfCell(table.Cell(), CashierShiftFormatting.SignedMoney(session.Variance));
                                PdfCell(table.Cell(), CashierShiftFormatting.Money(session.OpeningCashFloat)); PdfCell(table.Cell(), CashierShiftFormatting.Money(session.CashRefunds)); PdfCell(table.Cell(), CashierShiftFormatting.Money(session.CashPayouts)); PdfCell(table.Cell(), (session.TransactionCount ?? 0).ToString());
                                PdfCell(table.Cell(), StoreDateTime.ToStoreTimeFromUtc(session.ClockedInAtUtc).ToString("h:mm tt")); PdfCell(table.Cell(), session.ClockedOutAtUtc is { } clockedOut ? StoreDateTime.ToStoreTimeFromUtc(clockedOut).ToString("h:mm tt") : "-");
                                PdfCell(table.Cell(), session.WorkedMinutes is { } minutes ? $"{minutes / 60}h {minutes % 60}m" : "-"); PdfCell(table.Cell(), session.CashFloatReturned == true ? "Yes" : "No");
                            }
                        });
                        break;
                    case ReportExportArea.Inventory:
                        content.Item().Text("Inventory by location").Bold().FontSize(13);
                        content.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns => { columns.ConstantColumn(50); columns.RelativeColumn(); columns.RelativeColumn(); columns.ConstantColumn(42); columns.ConstantColumn(42); columns.ConstantColumn(42); columns.ConstantColumn(62); });
                            table.Header(header => { PdfHeader(header.Cell(), "SKU"); PdfHeader(header.Cell(), "Product"); PdfHeader(header.Cell(), "Supplier"); PdfHeader(header.Cell(), "Display"); PdfHeader(header.Cell(), "Bodega"); PdfHeader(header.Cell(), "Total"); PdfHeader(header.Cell(), "Status"); });
                            foreach (var product in report.Inventory.Products.OrderBy(product => product.Name))
                            {
                                PdfCell(table.Cell(), product.Sku); PdfCell(table.Cell(), product.Name); PdfCell(table.Cell(), product.SupplierName); PdfCell(table.Cell(), product.DisplayStock.ToString());
                                PdfCell(table.Cell(), product.BodegaStock.ToString()); PdfCell(table.Cell(), product.TotalStock.ToString()); PdfCell(table.Cell(), product.StockStatus);
                            }
                        });
                        break;
                    case ReportExportArea.Orders:
                        content.Item().Text("Suggested orders").Bold().FontSize(13);
                        foreach (var supplier in report.Orders.Suppliers)
                        {
                            content.Item().Text(supplier.SupplierName).Bold().FontSize(10);
                            content.Item().Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.ConstantColumn(58); columns.RelativeColumn(); columns.ConstantColumn(48); columns.ConstantColumn(48);
                                    columns.ConstantColumn(48); columns.ConstantColumn(58); columns.ConstantColumn(58); columns.ConstantColumn(58);
                                });
                                table.Header(header =>
                                {
                                    PdfHeader(header.Cell(), "SKU"); PdfHeader(header.Cell(), "Product"); PdfHeader(header.Cell(), "Bodega"); PdfHeader(header.Cell(), "Display");
                                    PdfHeader(header.Cell(), "Total"); PdfHeader(header.Cell(), "Critical level"); PdfHeader(header.Cell(), "Warning level"); PdfHeader(header.Cell(), "How many to order");
                                });
                                foreach (var product in supplier.Products)
                                {
                                    PdfCell(table.Cell(), product.Sku); PdfCell(table.Cell(), product.ProductName); PdfCell(table.Cell(), product.BodegaStock.ToString());
                                    PdfCell(table.Cell(), product.DisplayStock.ToString()); PdfCell(table.Cell(), product.TotalStock.ToString());
                                    PdfCell(table.Cell(), product.CriticalReorderLevel.ToString()); PdfCell(table.Cell(), product.WarningReorderLevel.ToString());
                                    PdfCell(table.Cell(), product.SuggestedOrderQuantity.ToString());
                                }
                            });
                        }
                        break;
                }
            });
            page.Footer().AlignCenter().Text(text => { text.Span("Page "); text.CurrentPageNumber(); text.Span(" of "); text.TotalPages(); });
        })).GeneratePdf(output);
    }

    public static void ExportExcel(ApiReportSnapshot report, Stream output, ReportExportArea area = ReportExportArea.All)
    {
        using var workbook = new XLWorkbook();
        switch (area)
        {
            case ReportExportArea.All:
                CreateSummarySheet(workbook, report);
                CreateSalesSheet(workbook, report.Sales);
                CreateInventorySheet(workbook, report.Inventory);
                CreateOrdersSheet(workbook, report.Orders);
                if (report.EmployeePurchases is not null) CreateEmployeePurchasesSheet(workbook, report.EmployeePurchases);
                if (report.CashierShifts is not null) CreateCashierRemittanceSheet(workbook, report.CashierShifts);
                if (report.SalesAccountability is not null)
                    foreach (var dates in report.SalesAccountability.Dates.Chunk(7))
                        CreateSalesAccountabilitySheet(workbook, report.SalesAccountability, dates);
                break;
            case ReportExportArea.Sales: CreateSalesSheet(workbook, report.Sales); break;
            case ReportExportArea.EmployeePurchases when report.EmployeePurchases is not null: CreateEmployeePurchasesSheet(workbook, report.EmployeePurchases); break;
            case ReportExportArea.CashierRemittance when report.CashierShifts is not null: CreateCashierRemittanceSheet(workbook, report.CashierShifts); break;
            case ReportExportArea.Inventory: CreateInventorySheet(workbook, report.Inventory); break;
            case ReportExportArea.Orders: CreateOrdersSheet(workbook, report.Orders); break;
            case ReportExportArea.SalesAccountability when report.SalesAccountability is not null:
                foreach (var dates in report.SalesAccountability.Dates.Chunk(7))
                    CreateSalesAccountabilitySheet(workbook, report.SalesAccountability, dates);
                break;
            default: CreateSummarySheet(workbook, report); break;
        }
        workbook.SaveAs(output);
    }

    public static void ExportSalesAccountabilityExcel(SalesAccountabilityReportResponse report, Stream output)
    {
        using var workbook = new XLWorkbook();
        foreach (var dates in report.Dates.Chunk(7))
            CreateSalesAccountabilitySheet(workbook, report, dates);
        workbook.SaveAs(output);
    }

    public static void ExportSalesAccountabilityPdf(SalesAccountabilityReportResponse report, Stream output)
    {
        var dateChunks = report.Dates.Chunk(7).ToArray();
        Document.Create(document =>
        {
            foreach (var dates in dateChunks)
            {
                document.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(26);
                    page.DefaultTextStyle(style => style.FontSize(8));
                    page.Header().Column(header =>
                    {
                        header.Item().Text("BPNV CONVENIENCE STORE").Bold().FontSize(18).FontColor(Colors.Orange.Darken2);
                        header.Item().Text("Sales Accountability Report").SemiBold().FontSize(12);
                        header.Item().Text($"{report.FromDate:MMM d, yyyy} to {report.ToDateExclusive.AddDays(-1):MMM d, yyyy}")
                            .FontSize(8).FontColor(Colors.Grey.Darken1);
                    });
                    page.Content().PaddingVertical(14).Column(content =>
                    {
                        content.Spacing(12);
                        content.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(1.4f);
                                foreach (var _ in dates) { columns.RelativeColumn(); columns.RelativeColumn(); }
                            });
                            table.Header(header =>
                            {
                                PdfHeader(header.Cell().RowSpan(2), "Category");
                                foreach (var date in dates)
                                    PdfHeaderCentered(header.Cell().ColumnSpan(2), $"{date:MMM d, yyyy}");
                                foreach (var _ in dates)
                                {
                                    PdfHeader(header.Cell(), "Regular Sales");
                                    PdfHeader(header.Cell(), "Employee Sales");
                                }
                            });
                            foreach (var category in report.Categories.Append("TOTAL SALES"))
                            {
                                PdfCell(table.Cell(), category);
                                foreach (var date in dates)
                                {
                                    var values = category == "TOTAL SALES"
                                        ? report.Sales.Where(cell => cell.BusinessDate == date).ToArray()
                                        : report.Sales.Where(cell => cell.BusinessDate == date && cell.Category == category).ToArray();
                                    PdfCell(table.Cell(), values.Sum(cell => cell.RegularSales).ToString("₱#,##0.00"));
                                    PdfCell(table.Cell(), values.Sum(cell => cell.EmployeeSales).ToString("₱#,##0.00"));
                                }
                            }
                        });

                        if (report.IncludesCashAccountability)
                        {
                            content.Item().Text("Daily cash accountability").Bold().FontSize(11);
                            content.Item().Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn(); columns.RelativeColumn(); columns.RelativeColumn();
                                    columns.RelativeColumn(); columns.RelativeColumn(); columns.RelativeColumn();
                                    columns.RelativeColumn(); columns.RelativeColumn(1.4f);
                                });
                                table.Header(header =>
                                {
                                    foreach (var label in new[] { "Business Date", "Total Sales", "Cash Sales", "GCash Payments", "Approved Expenses", "Cash Remitted", "Cash Difference", "Assigned Cashiers" })
                                        PdfHeader(header.Cell(), label);
                                });
                                foreach (var day in report.Days.Where(day => dates.Contains(day.BusinessDate)))
                                {
                                    PdfCell(table.Cell(), day.DateDisplay); PdfCell(table.Cell(), day.TotalSales.ToString("₱#,##0.00"));
                                    PdfCell(table.Cell(), day.CashSales.ToString("₱#,##0.00")); PdfCell(table.Cell(), day.GCashPayments.ToString("₱#,##0.00"));
                                    PdfCell(table.Cell(), day.ApprovedExpenses.ToString("₱#,##0.00")); PdfCell(table.Cell(), MoneyOrDash(day.CashRemitted));
                                    PdfCell(table.Cell(), MoneyOrDash(day.Variance)); PdfCell(table.Cell(), day.CashiersDisplay);
                                }
                            });
                        }
                    });
                    page.Footer().AlignCenter().Text(text => { text.Span("Page "); text.CurrentPageNumber(); text.Span(" of "); text.TotalPages(); });
                });
            }
        }).GeneratePdf(output);
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
        string[] headers = ["Sale #", "Date & time", "Sale type", "Payment method", "Cashier", "SKU", "Product / unit", "Price", "Quantity", "Amount"];
        WriteHeaders(sheet, headers);

        var row = 2;
        foreach (var sale in sales.Sales.OrderByDescending(sale => sale.SoldAtUtc))
        {
            var firstLine = true;
            foreach (var line in sale.Lines)
            {
                sheet.Cell(row, 1).Value = firstLine ? sale.SaleNumber : "";
                if (firstLine)
                {
                    sheet.Cell(row, 2).Value = StoreDateTime.ToStoreTimeFromUtc(sale.SoldAtUtc);
                    sheet.Cell(row, 3).Value = sale.CustomerType.ToString();
                    sheet.Cell(row, 4).Value = sale.PaymentMethodDisplay;
                    sheet.Cell(row, 5).Value = sale.SoldByName;
                }
                sheet.Cell(row, 6).Value = line.Sku;
                sheet.Cell(row, 7).Value = $"{line.ProductName} ({line.UnitLabel})";
                sheet.Cell(row, 8).Value = line.UnitPrice;
                sheet.Cell(row, 9).Value = line.BasePieceQuantity;
                sheet.Cell(row, 10).Value = line.LineTotal;
                row++;
                firstLine = false;
            }
        }

        sheet.Column(2).Style.DateFormat.Format = StoreDateTime.ExcelTimestampFormat;
        sheet.Column(8).Style.NumberFormat.Format = "₱#,##0.00";
        sheet.Column(10).Style.NumberFormat.Format = "₱#,##0.00";
        StyleDataSheet(sheet, headers.Length, row - 1);

        // Keep report columns readable even when the data values are short.
        var widths = new[] { 18, 32, 14, 18, 20, 18, 28, 12, 12, 14 };
        for (var column = 1; column <= widths.Length; column++)
            sheet.Column(column).Width = widths[column - 1];
        sheet.Range(1, 1, row - 1, headers.Length).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        sheet.Range(1, 1, 1, headers.Length).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
        sheet.Column(2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
        // The Excel table already repeats its header while scrolling; avoid a second frozen pane.
        sheet.SheetView.FreezeRows(0);
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
        var widths = new[] { 18, 30, 24, 16, 18, 12, 12, 12, 12, 16, 16, 16, 16, 16, 16, 16, 16, 16, 18 };
        for (var column = 1; column <= widths.Length; column++)
            sheet.Column(column).Width = widths[column - 1];
        sheet.Range(1, 1, row - 1, headers.Length).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        sheet.Range(1, 1, 1, headers.Length).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
        sheet.Columns(1, 6).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
        sheet.Columns(7, 18).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
        sheet.Column(19).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
        sheet.SheetView.FreezeRows(0);
    }

    private static void CreateOrdersSheet(XLWorkbook workbook, OrderReportResponse orders)
    {
        var sheet = workbook.Worksheets.Add("Orders");
        string[] headers = ["Supplier", "SKU", "Product", "Bodega", "Display", "Total", "Critical level", "Warning level", "How many to order"];
        WriteHeaders(sheet, headers);
        var row = 2;
        foreach (var supplier in orders.Suppliers)
        {
            var supplierStart = row;
            foreach (var product in supplier.Products)
            {
                sheet.Cell(row, 1).Value = row == supplierStart ? supplier.SupplierName : string.Empty;
                sheet.Cell(row, 2).Value = product.Sku;
                sheet.Cell(row, 3).Value = product.ProductName;
                sheet.Cell(row, 4).Value = product.BodegaStock;
                sheet.Cell(row, 5).Value = product.DisplayStock;
                sheet.Cell(row, 6).Value = product.TotalStock;
                sheet.Cell(row, 7).Value = product.CriticalReorderLevel;
                sheet.Cell(row, 8).Value = product.WarningReorderLevel;
                sheet.Cell(row, 9).Value = product.SuggestedOrderQuantity;
                row++;
            }
        }
        StyleDataSheet(sheet, headers.Length, row - 1);
        var widths = new[] { 28, 18, 28, 14, 14, 14, 16, 16, 20 };
        for (var column = 1; column <= widths.Length; column++)
            sheet.Column(column).Width = widths[column - 1];
        sheet.Range(1, 1, row - 1, headers.Length).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        sheet.Range(1, 1, 1, headers.Length).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
        sheet.Columns(1, 3).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
        sheet.Columns(4, 9).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
        sheet.SheetView.FreezeRows(0);
    }

    private static void CreateEmployeePurchasesSheet(XLWorkbook workbook, EmployeePurchaseReportResponse report)
    {
        var sheet = workbook.Worksheets.Add("Employee Purchases");
        string[] headers = ["Employee", "Sale #", "Date & time", "SKU", "Product / unit", "Price", "Quantity", "Line total", "Paid", "Owed"];
        WriteHeaders(sheet, headers);
        var row = 2;
        var displayedEmployees = new HashSet<string>();
        foreach (var employee in report.Lines.GroupBy(line => line.EmployeeDisplay).OrderBy(group => group.Key))
        foreach (var sale in employee.GroupBy(line => new { line.SaleId, line.SaleNumber, line.SoldAtUtc, line.EmployeeDisplay }).OrderByDescending(group => group.Key.SoldAtUtc))
        {
            var firstLine = true;
            foreach (var line in sale)
            {
                sheet.Cell(row, 1).Value = firstLine && displayedEmployees.Add(line.EmployeeDisplay) ? line.EmployeeDisplay : "";
                sheet.Cell(row, 2).Value = firstLine ? line.SaleNumber : "";
                if (firstLine)
                    sheet.Cell(row, 3).Value = StoreDateTime.ToStoreTimeFromUtc(line.SoldAtUtc);
                sheet.Cell(row, 4).Value = line.Sku;
                sheet.Cell(row, 5).Value = $"{line.ProductName} ({line.UnitLabel})";
                sheet.Cell(row, 6).Value = line.UnitPrice;
                sheet.Cell(row, 7).Value = line.BasePieceQuantity;
                sheet.Cell(row, 8).Value = line.LineTotal;
                if (line == sale.Last())
                {
                    sheet.Cell(row, 9).Value = line.IsOwed ? 0m : line.SaleTotal;
                    sheet.Cell(row, 10).Value = line.IsOwed ? line.SaleTotal : 0m;
                }
                row++;
                firstLine = false;
            }
        }
        StyleDataSheet(sheet, headers.Length, row - 1);
        sheet.Column(3).Style.DateFormat.Format = StoreDateTime.ExcelTimestampFormat;
        sheet.Column(6).Style.NumberFormat.Format = "₱#,##0.00";
        sheet.Column(8).Style.NumberFormat.Format = "₱#,##0.00";
        sheet.Columns(9, 10).Style.NumberFormat.Format = "₱#,##0.00";

        var widths = new[] { 30, 18, 32, 18, 28, 14, 12, 16, 16, 16 };
        for (var column = 1; column <= widths.Length; column++)
            sheet.Column(column).Width = widths[column - 1];
        sheet.Range(1, 1, row - 1, headers.Length).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        sheet.Range(1, 1, 1, headers.Length).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
        sheet.Column(3).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
        sheet.SheetView.FreezeRows(0);
        var summaryRow = row + 1;
        sheet.Cell(summaryRow, 1).Value = "Employee";
        sheet.Cell(summaryRow, 2).Value = "Owed";
        sheet.Range(summaryRow, 1, summaryRow, 2).Style.Font.Bold = true;
        foreach (var employee in report.Lines.GroupBy(line => line.EmployeeDisplay).OrderBy(group => group.Key))
        {
            summaryRow++;
            sheet.Cell(summaryRow, 1).Value = employee.Key;
            sheet.Cell(summaryRow, 2).Value = employee.Where(line => line.IsOwed).GroupBy(line => line.SaleId).Sum(sale => sale.First().SaleTotal);
            sheet.Cell(summaryRow, 2).Style.NumberFormat.Format = "₱#,##0.00";
        }
    }

    private static void CreateCashierRemittanceSheet(XLWorkbook workbook, CashierShiftReportResponse report)
    {
        var sheet = workbook.Worksheets.Add("Cashier Remittance");
        string[] headers = ["Business date", "Cashier", "Shift", "Status", "Sales", "Cash collected", "Expected cash", "Actual cash", "Remittance difference", "Starting cash", "Cash refunds", "Cash payouts", "Sales count", "Shift start", "Shift end", "Shift length", "Cash returned"];
        WriteHeaders(sheet, headers);
        var row = 2;
        foreach (var session in report.Sessions.OrderByDescending(session => session.BusinessDate).ThenBy(session => session.CashierName))
        {
            sheet.Cell(row, 1).Value = session.BusinessDate.ToDateTime(TimeOnly.MinValue);
            sheet.Cell(row, 2).Value = session.CashierName;
            sheet.Cell(row, 3).Value = session.ShiftName;
            sheet.Cell(row, 4).Value = session.Status == ApiCashierShiftSessionStatus.ClosedPendingRemittance ? "Pending Remittance" : session.Status.ToString();
            sheet.Cell(row, 5).Value = session.TotalSales ?? 0;
            sheet.Cell(row, 6).Value = session.CashSales ?? 0;
            sheet.Cell(row, 7).Value = session.ExpectedRemittance ?? 0;
            sheet.Cell(row, 8).Value = session.ActualRemittance ?? 0;
            sheet.Cell(row, 9).Value = session.Variance ?? 0;
            sheet.Cell(row, 10).Value = session.OpeningCashFloat;
            sheet.Cell(row, 11).Value = session.CashRefunds ?? 0;
            sheet.Cell(row, 12).Value = session.CashPayouts ?? 0;
            sheet.Cell(row, 13).Value = session.TransactionCount ?? 0;
            sheet.Cell(row, 14).Value = StoreDateTime.ToStoreTimeFromUtc(session.ClockedInAtUtc);
            if (session.ClockedOutAtUtc is { } clockedOut)
                sheet.Cell(row, 15).Value = StoreDateTime.ToStoreTimeFromUtc(clockedOut);
            sheet.Cell(row, 16).Value = session.WorkedMinutes is { } minutes
                ? $"{minutes / 60}h {minutes % 60}m"
                : "-";
            sheet.Cell(row, 17).Value = session.CashFloatReturned == true ? "Yes" : "No";
            row++;
        }
        sheet.Column(1).Style.DateFormat.Format = "mmmm d, yyyy";
        sheet.Columns(5, 12).Style.NumberFormat.Format = "₱#,##0.00";
        StyleDataSheet(sheet, headers.Length, row - 1);
        var widths = new[] { 18, 24, 22, 20, 16, 16, 20, 16, 16, 16, 14, 14, 14, 24, 24, 16, 20 };
        for (var column = 1; column <= widths.Length; column++)
            sheet.Column(column).Width = widths[column - 1];
        sheet.Range(1, 1, row - 1, headers.Length).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        sheet.Range(1, 1, 1, headers.Length).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
        sheet.Column(1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
        sheet.Columns(14, 15).Style.DateFormat.Format = "h:mm AM/PM";
        sheet.SheetView.FreezeRows(0);
    }

    private static void CreateSalesAccountabilitySheet(
        XLWorkbook workbook,
        SalesAccountabilityReportResponse report,
        IReadOnlyList<DateOnly> dates)
    {
        var firstDate = dates[0];
        var lastDate = dates[^1];
        var sheetName = dates.Count == 1
            ? firstDate.ToString("MMM d")
            : $"{firstDate:MMM d}-{lastDate:MMM d}";
        var sheet = workbook.Worksheets.Add(sheetName[..Math.Min(sheetName.Length, 31)]);
        var lastColumn = 1 + dates.Count * 2;
        sheet.Range(1, 1, 1, lastColumn).Merge();
        sheet.Cell(1, 1).Value = "BPNV SALES ACCOUNTABILITY REPORT";
        sheet.Cell(1, 1).Style.Font.SetBold().Font.SetFontSize(16);
        sheet.Cell(2, 1).Value = "Date range";
        sheet.Cell(2, 2).Value = $"{report.FromDate:MMM d, yyyy} to {report.ToDateExclusive.AddDays(-1):MMM d, yyyy}";
        sheet.Cell(4, 1).Value = "Category";
        var column = 2;
        foreach (var date in dates)
        {
            sheet.Range(4, column, 4, column + 1).Merge();
            sheet.Cell(4, column).Value = date.ToDateTime(TimeOnly.MinValue);
            sheet.Cell(4, column).Style.DateFormat.Format = "mmm d, yyyy";
            sheet.Cell(5, column).Value = "Regular Sales";
            sheet.Cell(5, column + 1).Value = "Employee Sales";
            column += 2;
        }

        var row = 6;
        foreach (var category in report.Categories.Append("TOTAL SALES"))
        {
            sheet.Cell(row, 1).Value = category;
            column = 2;
            foreach (var date in dates)
            {
                var values = category == "TOTAL SALES"
                    ? report.Sales.Where(cell => cell.BusinessDate == date).ToArray()
                    : report.Sales.Where(cell => cell.BusinessDate == date && cell.Category == category).ToArray();
                sheet.Cell(row, column).Value = values.Sum(cell => cell.RegularSales);
                sheet.Cell(row, column + 1).Value = values.Sum(cell => cell.EmployeeSales);
                column += 2;
            }
            row++;
        }
        sheet.Range(6, 2, row - 1, lastColumn).Style.NumberFormat.Format = "₱#,##0.00";
        column = 2;
        foreach (var _ in dates)
        {
            sheet.Range(4, column, 4, column + 1).Style.DateFormat.Format = "mmm d, yyyy";
            column += 2;
        }
        sheet.Range(4, 1, 5, lastColumn).Style.Font.Bold = true;
        sheet.Range(4, 1, 5, lastColumn).Style.Fill.BackgroundColor = XLColor.FromHtml("#F59E0B");
        sheet.Range(4, 1, row - 1, lastColumn).Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        sheet.Range(4, 1, row - 1, lastColumn).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;

        if (!report.IncludesCashAccountability)
        {
            sheet.Column(1).Width = 20;
            for (var index = 2; index <= lastColumn; index++) sheet.Column(index).Width = 18;
            return;
        }

        row += 2;
        string[] headers = ["Business date", "Total sales", "Cash sales", "GCash payments", "Employee owed", "Approved expenses", "Cash remitted", "Expected cash", "Actual cash", "Cash difference", "Assigned cashiers"];
        for (var index = 0; index < headers.Length; index++) sheet.Cell(row, index + 1).Value = headers[index];
        sheet.Range(row, 1, row, headers.Length).Style.Font.Bold = true;
        sheet.Range(row, 1, row, headers.Length).Style.Fill.BackgroundColor = XLColor.FromHtml("#F59E0B");
        foreach (var day in report.Days)
        {
            row++;
            sheet.Cell(row, 1).Value = day.BusinessDate.ToDateTime(TimeOnly.MinValue);
            sheet.Cell(row, 2).Value = day.TotalSales; sheet.Cell(row, 3).Value = day.CashSales;
            sheet.Cell(row, 4).Value = day.GCashPayments; sheet.Cell(row, 5).Value = day.EmployeeOwedSales;
            sheet.Cell(row, 6).Value = day.ApprovedExpenses;
            SetNullableMoney(sheet.Cell(row, 7), day.CashRemitted); SetNullableMoney(sheet.Cell(row, 8), day.ExpectedCash);
            SetNullableMoney(sheet.Cell(row, 9), day.ActualCash); SetNullableMoney(sheet.Cell(row, 10), day.Variance);
            sheet.Cell(row, 11).Value = day.CashiersDisplay;
        }
        sheet.Column(1).Style.DateFormat.Format = "mmm d, yyyy";
        sheet.Column(1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
        sheet.Columns(2, 10).Style.NumberFormat.Format = "₱#,##0.00";
        column = 2;
        foreach (var date in dates)
        {
            sheet.Range(4, column, 4, column + 1).Style.NumberFormat.Format = "mmm d, yyyy";
            sheet.Range(4, column, 4, column + 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            sheet.Cell(4, column).Value = date.ToDateTime(TimeOnly.MinValue);
            column += 2;
        }
        sheet.Column(1).Width = 18;
        for (var index = 2; index <= 10; index++) sheet.Column(index).Width = 16;
        sheet.Column(11).Width = 24;
    }

    private static void SetNullableMoney(IXLCell cell, decimal? value)
    {
        if (value.HasValue) cell.Value = value.Value;
        else cell.Value = "-";
    }

    private static string MoneyOrDash(decimal? value) => value.HasValue ? value.Value.ToString("₱#,##0.00") : "-";

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

    private static void PdfHeaderCentered(IContainer container, string text) => container
        .Background(Colors.Orange.Medium).Padding(5).Text(text).AlignCenter().Bold().FontSize(8);

    private static void PdfCell(IContainer container, string text) => container
        .BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(text).FontSize(8);
}
