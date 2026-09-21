using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Layout;
using AvaloniaApp.Services;
using AvaloniaApp.ViewModels;
using AvaloniaApp.Views.UI;
using AvaloniaApp.Views.Dialogs;

namespace AvaloniaApp.Views;

public sealed class CashierSalesView : UserControl
{
    public CashierSalesView()
    {
        var table = new PagedTable { ItemName = "transaction", ItemNamePlural = "transactions", PageSize = 10, IsSelectable = true, MinHeight = 360 };
        table.Bind(PagedTable.ItemsSourceProperty, new Binding("Sales"));
        table.Bind(PagedTable.SelectedItemProperty, new Binding("SelectedSale") { Mode = BindingMode.TwoWay });
        table.PropertyChanged += async (_, change) =>
        {
            if (change.Property != PagedTable.SelectedItemProperty || table.SelectedItem is not ReportSaleResponse sale || TopLevel.GetTopLevel(table) is not Window owner) return;
            await new SaleDetailDialog(sale).ShowDialog(owner);
            table.SelectedItem = null;
        };
        table.Columns.Add(PagedTableColumn.Create<ReportSaleResponse, string>("TIMESTAMP", sale => sale.TimeDisplay, new GridLength(1.4, GridUnitType.Star)));
        table.Columns.Add(PagedTableColumn.Create<ReportSaleResponse, string>("SALE", sale => sale.SaleNumber, new GridLength(1, GridUnitType.Star)));
        table.Columns.Add(PagedTableColumn.Create<ReportSaleResponse, string>("CUSTOMER", sale => sale.CustomerType.ToString(), new GridLength(0.9, GridUnitType.Star)));
        table.Columns.Add(PagedTableColumn.Create<ReportSaleResponse, string>("PAYMENT", sale => sale.PaymentMethodDisplay, new GridLength(1.4, GridUnitType.Star)));
        table.Columns.Add(PagedTableColumn.Create<ReportSaleResponse, int>("ITEMS", sale => sale.ItemCount, new GridLength(0.6, GridUnitType.Star)));
        table.Columns.Add(PagedTableColumn.Create<ReportSaleResponse, string>("TOTAL", sale => sale.TotalDisplay, new GridLength(0.8, GridUnitType.Star)));
        Content = new StackPanel { Margin = new Avalonia.Thickness(30), Spacing = 18, Children =
        {
            Status(),
            new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,*,*,*,*"), Children =
            {
                Card("TOTAL SALES", "TotalDisplay", 0), Card("TRANSACTIONS", "TransactionsDisplay", 1), Card("ITEMS SOLD", "UnitsDisplay", 2), Card("CASH", "CashDisplay", 3), Card("GCASH", "GCashDisplay", 4), Card("OWED", "OwedDisplay", 5)
            }},
            table
        }};
    }
    private static Control Status() { var text = new TextBlock(); text.Bind(TextBlock.TextProperty, new Binding("StatusMessage")); return text; }
    private static Control Card(string label, string path, int column)
    {
        var value = new TextBlock { FontSize = 22, FontWeight = Avalonia.Media.FontWeight.SemiBold };
        value.Bind(TextBlock.TextProperty, new Binding(path));
        var card = new Border { Padding = new Avalonia.Thickness(16), Margin = new Avalonia.Thickness(4), Child = new StackPanel { Spacing = 5, Children = { new TextBlock { Text = label, FontSize = 11 }, value } } };
        card.Bind(Border.BackgroundProperty, new Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension("Card"));
        card.Bind(Border.BorderBrushProperty, new Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension("Border"));
        card.BorderThickness = new Avalonia.Thickness(1);
        Grid.SetColumn(card, column);
        return card;
    }
}
