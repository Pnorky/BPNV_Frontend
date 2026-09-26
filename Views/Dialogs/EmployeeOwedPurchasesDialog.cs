using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using AvaloniaApp.Services;
using AvaloniaApp.Views.UI;

namespace AvaloniaApp.Views.Dialogs;

public sealed class EmployeeOwedPurchasesDialog : Window
{
    public EmployeeOwedPurchasesDialog(EmployeeBalanceResponse employee, IReadOnlyList<EmployeeOwedPurchaseResponse> purchases)
    {
        Title = $"Outstanding Purchases - {employee.EmployeeName}";
        Width = 820; Height = 520; MinWidth = 680; MinHeight = 400;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        this.BindResource(BackgroundProperty, "Card"); this.BindResource(ForegroundProperty, "Foreground");
        var table = new PagedTable { ItemsSource = purchases, ItemName = "owed purchase", ItemNamePlural = "owed purchases", PageSize = 10, MinTableWidth = 700 };
        table.Columns.Add(PagedTableColumn.Create<EmployeeOwedPurchaseResponse, string>("SALE", item => item.SaleNumber));
        table.Columns.Add(PagedTableColumn.Create<EmployeeOwedPurchaseResponse, DateTime>("DATE", item => item.SoldAtUtc));
        table.Columns.Add(PagedTableColumn.Create<EmployeeOwedPurchaseResponse, decimal>("ORIGINAL", item => item.OriginalAmount));
        table.Columns.Add(PagedTableColumn.Create<EmployeeOwedPurchaseResponse, decimal>("PAID", item => item.PaidAmount));
        table.Columns.Add(PagedTableColumn.Create<EmployeeOwedPurchaseResponse, decimal>("OUTSTANDING", item => item.OutstandingAmount));
        var close = new ActionButton("Close", ActionButtonVariant.Secondary); close.Click += (_, _) => Close();
        Content = new Grid { Margin = new Thickness(24), RowDefinitions = new RowDefinitions("Auto,*,Auto"), RowSpacing = 16, Children =
        {
            new StackPanel { Spacing = 3, Children = { new TextBlock { Text = "Outstanding purchases", Classes = { "h2" } }, new TextBlock { Text = employee.EmployeeDisplay } } },
            At(table, 1), At(new StackPanel { HorizontalAlignment = HorizontalAlignment.Right, Children = { close } }, 2)
        } };
    }
    private static T At<T>(T value, int row) where T : Control { Grid.SetRow(value, row); return value; }
}
