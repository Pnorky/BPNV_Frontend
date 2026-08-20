using Avalonia;
using Avalonia.Controls;
using AvaloniaApp.Views.UI;

namespace AvaloniaApp.Views;

public class BillingView : UserControl
{
    public BillingView()
    {
        var actions = new DockPanel { Margin = new Thickness(0, 0, 0, 20) };
        var button = ViewCode.Bind(new ActionButton("New Invoice"),
            Button.CommandProperty, "NewInvoiceCommand");
        DockPanel.SetDock(button, Dock.Right);
        actions.Children.Add(button);
        var search = new IconInput("Search", "Search invoice or patient...") { Margin = new Thickness(0, 0, 10, 0) };
        ViewCode.Bind(search.Input, TextBox.TextProperty, "SearchText");
        actions.Children.Add(search);
        Grid.SetRow(actions, 1);

        var table = ViewCode.Table("FilteredInvoices", "invoice", "invoices");
        ViewCode.Bind(table, PagedTable.IsFilteredProperty, "HasSearch");
        ViewCode.Bind(table, PagedTable.ClearFiltersCommandProperty, "ClearSearchCommand");
        table.EmptyActionText = "Create Invoice";
        ViewCode.Bind(table, PagedTable.EmptyActionCommandProperty, "NewInvoiceCommand");
        ViewCode.AddColumns(table,
            ViewCode.Column("Invoice #", "InvoiceNo"),
            ViewCode.Column("Patient", "Patient", 1.4),
            ViewCode.Column("Service", "Service", 1.4),
            ViewCode.Column("Amount", "Amount", 0.9),
            ViewCode.Column("Insurance", "Insurance"),
            ViewCode.Column("Status", "Status", 1.2, ViewCode.StatusTemplate<InvoiceRecord>("Status")));
        var border = ViewCode.Resource(new Border
        {
            CornerRadius = new CornerRadius(8),
            ClipToBounds = true,
            Child = table
        }, Border.BackgroundProperty, "Card");
        Grid.SetRow(border, 2);

        var root = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,*"), Margin = new Thickness(30) };
        root.Children.Add(ViewCode.Heading("Billing & Cashier"));
        root.Children.Add(actions);
        root.Children.Add(border);
        Content = root;
    }
}
