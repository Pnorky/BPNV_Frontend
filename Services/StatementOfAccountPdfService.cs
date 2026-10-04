using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace AvaloniaApp.Services;

public static class StatementOfAccountPdfService
{
    private static readonly Lazy<byte[]> Logo = new(() =>
    {
        var assembly = typeof(StatementOfAccountPdfService).Assembly;
        var resourceName = assembly.GetManifestResourceNames().Single(name =>
            name.EndsWith("petron-corporation-logo-png_seeklogo-472322.png", StringComparison.Ordinal));
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException("The Petron logo resource is missing.");
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    });

    public static void Export(CustomerStatementResponse statement, Stream output)
    {
        Document.Create(document => document.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(20);
            page.DefaultTextStyle(style => style.FontSize(9).FontFamily(Fonts.Arial));
            page.Header().Element(container => Header(container, statement));
            page.Content().PaddingTop(12).Column(content =>
            {
                content.Spacing(10);
                content.Item().AlignCenter().Text("STATEMENT OF ACCOUNT").Bold().FontSize(19);
                content.Item().AlignCenter().Text(statement.Customer.Name.ToUpperInvariant()).Bold().FontSize(13);
                content.Item().AlignCenter().Text($"For the Period: {statement.FromDate:MM/dd/yyyy} to {statement.ToDate:MM/dd/yyyy}").Bold().FontSize(11);
                content.Item().Element(container => ChargesTable(container, statement.Charges));
                content.Item().Element(container => Totals(container, statement));
                content.Item().Element(container => Payments(container, statement.Payments));
                content.Item().PaddingTop(8).Element(Signatures);
                content.Item().PaddingTop(12).Text("NOTE: CHECK must be payable to Bayombong Petro-NV Fuel Station").Bold().FontSize(11);
            });
            page.Footer().Row(row =>
            {
                row.RelativeItem().AlignLeft().Text("System Generated Reports").Italic().Bold().FontSize(8);
                row.RelativeItem().AlignRight().Text(text =>
                {
                    text.Span("Page "); text.CurrentPageNumber(); text.Span(" of "); text.TotalPages();
                });
            });
        })).GeneratePdf(output);
    }

    private static void Header(IContainer container, CustomerStatementResponse statement) => container.Row(row =>
    {
        row.ConstantItem(82).Height(88).Image(Logo.Value).FitArea();
        row.RelativeItem().PaddingLeft(10).PaddingTop(10).Column(column =>
        {
            column.Item().Text("Bayombong Petro-NV Fuel Station").Bold().FontSize(16);
            column.Item().Text("By-Pass Road, Brgy. Bonfal East, Bayombong, Nueva Vizcaya").Bold().FontSize(11);
            column.Item().PaddingTop(5).Text($"Generated {StoreDateTime.FormatUtc(statement.GeneratedAtUtc)}").FontSize(7).FontColor(Colors.Grey.Darken1);
        });
    });

    private static void ChargesTable(IContainer container, IReadOnlyList<CustomerStatementLineResponse> charges) => container.Table(table =>
    {
        table.ColumnsDefinition(columns =>
        {
            columns.ConstantColumn(58); columns.RelativeColumn(2.2f); columns.ConstantColumn(70);
            columns.ConstantColumn(65); columns.ConstantColumn(55); columns.ConstantColumn(57);
            columns.ConstantColumn(55); columns.ConstantColumn(65);
        });
        table.Header(header =>
        {
            HeaderCell(header.Cell(), "Date"); HeaderCell(header.Cell(), "Particular");
            HeaderCell(header.Cell(), "Invoice/Ref No"); HeaderCell(header.Cell(), "Plate No.");
            HeaderCell(header.Cell(), "Qty/Liters"); HeaderCell(header.Cell(), "Unit Price");
            HeaderCell(header.Cell(), "Disc Amt"); HeaderCell(header.Cell(), "Net Amount");
        });
        if (charges.Count == 0)
        {
            table.Cell().ColumnSpan(8).BorderBottom(0.5f).Padding(7).AlignCenter().Text("No current charges for the selected period.").Italic();
            return;
        }
        foreach (var line in charges)
        {
            Cell(table.Cell(), StoreDateTime.ToStoreTimeFromUtc(line.SoldAtUtc).ToString("MM/dd/yyyy"));
            Cell(table.Cell(), line.Particular, false); Cell(table.Cell(), line.InvoiceOrReference);
            Cell(table.Cell(), line.PlateOrUnitNumber ?? "-"); Cell(table.Cell(), line.Quantity.ToString("N2"), true);
            Cell(table.Cell(), line.UnitPrice.ToString("N2"), true); Cell(table.Cell(), line.DiscountAmount.ToString("N2"), true);
            Cell(table.Cell(), line.NetAmount.ToString("N2"), true);
        }
    });

    private static void Totals(IContainer container, CustomerStatementResponse value) => container.AlignRight().Width(310).Column(column =>
    {
        SummaryRow(column, "Total Current Charges:", value.TotalCurrentCharges);
        SummaryRow(column, "Previous Balance:", value.PreviousBalance);
        SummaryRow(column, "Payments for Period:", -value.TotalPeriodPayments);
        SummaryRow(column, "Total Balance:", value.TotalBalance, true);
    });

    private static void Payments(IContainer container, IReadOnlyList<CustomerPaymentResponse> payments) => container.Column(column =>
    {
        column.Item().Border(0.7f).Padding(3).AlignCenter().Text("Payments Made for selected Period").Bold().FontSize(11);
        column.Item().Table(table =>
        {
            table.ColumnsDefinition(columns => { columns.ConstantColumn(75); columns.ConstantColumn(90); columns.ConstantColumn(70); columns.RelativeColumn(); columns.ConstantColumn(75); });
            table.Header(header =>
            {
                HeaderCell(header.Cell(), "Date"); HeaderCell(header.Cell(), "Payment No."); HeaderCell(header.Cell(), "Method");
                HeaderCell(header.Cell(), "Reference"); HeaderCell(header.Cell(), "Amount");
            });
            if (payments.Count == 0)
                table.Cell().ColumnSpan(5).BorderBottom(0.5f).Padding(6).AlignCenter().Text("No payments recorded for the selected period.").Italic();
            foreach (var payment in payments)
            {
                Cell(table.Cell(), StoreDateTime.ToStoreTimeFromUtc(payment.PaidAtUtc).ToString("MM/dd/yyyy"));
                Cell(table.Cell(), payment.PaymentNumber); Cell(table.Cell(), payment.PaymentMethod.ToString());
                Cell(table.Cell(), payment.ReferenceNumber ?? "-", false); Cell(table.Cell(), payment.Amount.ToString("N2"), true);
            }
        });
    });

    private static void Signatures(IContainer container) => container.Column(column =>
    {
        column.Spacing(10);
        column.Item().Text("Prepared By: ____________________________").FontSize(11);
        column.Item().Text("Checked By:  ____________________________").FontSize(11);
        column.Item().Row(row =>
        {
            row.AutoItem().Text("Received by:   ").FontSize(11);
            row.ConstantItem(260).Column(signature =>
            {
                signature.Item().BorderBottom(0.7f).Height(14);
                signature.Item().AlignCenter().Text("Signature Over Printed Name").FontSize(9);
            });
        });
        column.Item().Text("Date Received: ____________________________").FontSize(11);
    });

    private static void SummaryRow(ColumnDescriptor column, string label, decimal amount, bool bold = false) => column.Item().Row(row =>
    {
        var left = row.RelativeItem().AlignRight().Text(label).FontSize(11);
        var right = row.ConstantItem(95).AlignRight().Text(amount.ToString("N2")).FontSize(11);
        if (bold) { left.Bold(); right.Bold(); }
        else { left.SemiBold(); right.SemiBold(); }
    });

    private static void HeaderCell(IContainer container, string text) => container.Border(0.6f).Padding(3).AlignCenter().Text(text).SemiBold().FontSize(8);
    private static void Cell(IContainer container, string text, bool right = false)
    {
        var cell = container.BorderBottom(0.5f).PaddingVertical(3).PaddingHorizontal(2);
        if (right) cell.AlignRight().Text(text).FontSize(8); else cell.Text(text).FontSize(8);
    }
}
