using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using AvaloniaApp.Services;
using AvaloniaApp.Views.UI;

namespace AvaloniaApp.Views.Dialogs;

public sealed class SaleDetailDialog : Window
{
    public SaleDetailDialog(ReportSaleResponse sale)
    {
        Title = $"Sale {sale.SaleNumber}";
        Width = 820;
        Height = 430;
        MinWidth = 760;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        this.BindResource(BackgroundProperty, "Card");
        this.BindResource(ForegroundProperty, "Foreground");

        var lines = new PagedTable { ItemName = "line item", ItemNamePlural = "line items", PageSize = 10, IsSelectable = false, MinHeight = 150, MinTableWidth = 0 };
        lines.ItemsSource = sale.Lines;
        lines.Columns.Add(PagedTableColumn.Create<ReportSaleLineResponse, string>("PRODUCT", line => $"{line.ProductName} · {line.Sku}", new GridLength(2, GridUnitType.Star)));
        lines.Columns.Add(PagedTableColumn.Create<ReportSaleLineResponse, string>("UNIT", line => line.UnitLabel, new GridLength(0.8, GridUnitType.Star)));
        lines.Columns.Add(PagedTableColumn.Create<ReportSaleLineResponse, int>("QTY", line => line.Count, new GridLength(0.5, GridUnitType.Star)));
        lines.Columns.Add(PagedTableColumn.Create<ReportSaleLineResponse, string>("UNIT PRICE", line => $"₱{line.UnitPrice:N2}", new GridLength(0.9, GridUnitType.Star)));
        lines.Columns.Add(PagedTableColumn.Create<ReportSaleLineResponse, string>("TOTAL", line => $"₱{line.LineTotal:N2}", new GridLength(0.9, GridUnitType.Star)));

        var content = new StackPanel { Margin = new Thickness(24), Spacing = 16, Children =
        {
            new TextBlock { Text = "Sale details", FontSize = 24, FontWeight = Avalonia.Media.FontWeight.Bold },
            new TextBlock { Text = $"{sale.SaleNumber} · {sale.TimeDisplay}", FontSize = 12 },
            new TextBlock { Text = $"{sale.CustomerType} customer · {sale.PaymentMethodDisplay}", FontSize = 12 },
            lines,
            new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = { new TextBlock { Text = "TOTAL", FontWeight = Avalonia.Media.FontWeight.Bold }, At(new TextBlock { Text = sale.TotalDisplay, FontSize = 22, FontWeight = Avalonia.Media.FontWeight.Bold }, 1) } },
            new Button { Content = "Close", HorizontalAlignment = HorizontalAlignment.Right }
        }};
        ((Button)content.Children[^1]).Click += (_, _) => Close();
        Content = content;
    }

    private static Control At(Control control, int column) { Grid.SetColumn(control, column); return control; }
}
