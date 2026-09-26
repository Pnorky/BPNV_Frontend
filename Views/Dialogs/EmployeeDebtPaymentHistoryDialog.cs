using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Layout;
using AvaloniaApp.Services;
using AvaloniaApp.Views.UI;

namespace AvaloniaApp.Views.Dialogs;

public sealed class EmployeeDebtPaymentHistoryDialog : Window
{
    public EmployeeDebtPaymentHistoryDialog(EmployeeBalanceResponse employee, IReadOnlyList<EmployeeDebtPaymentResponse> payments)
    {
        Title = $"Payment History - {employee.EmployeeName}";
        Width = 900;
        Height = 560;
        MinWidth = 720;
        MinHeight = 420;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        this.BindResource(BackgroundProperty, "Card");
        this.BindResource(ForegroundProperty, "Foreground");

        var table = new PagedTable { ItemsSource = payments, ItemName = "payment", ItemNamePlural = "payments", PageSize = 10, MinTableWidth = 780 };
        table.Columns.Add(PagedTableColumn.Create<EmployeeDebtPaymentResponse, string>("DATE & TIME", item => item.PaidAtDisplay, new GridLength(1.4, GridUnitType.Star)));
        table.Columns.Add(PagedTableColumn.Create<EmployeeDebtPaymentResponse, string>("AMOUNT", item => item.AmountDisplay, new GridLength(.8, GridUnitType.Star)));
        table.Columns.Add(PagedTableColumn.Create<EmployeeDebtPaymentResponse, string>("METHOD", item => item.PaymentMethod.ToString(), new GridLength(.7, GridUnitType.Star)));
        table.Columns.Add(PagedTableColumn.Create<EmployeeDebtPaymentResponse, string>("REFERENCE", item => item.ReferenceDisplay, new GridLength(1, GridUnitType.Star)));
        table.Columns.Add(PagedTableColumn.Create<EmployeeDebtPaymentResponse, string>("RECORDED BY", item => item.RecordedByName, new GridLength(1.1, GridUnitType.Star)));
        table.Columns.Add(PagedTableColumn.Create<EmployeeDebtPaymentResponse, string>("NOTE", item => item.NoteDisplay, new GridLength(1.2, GridUnitType.Star)));
        var close = new ActionButton("Close", ActionButtonVariant.Secondary);
        close.Click += (_, _) => Close();
        Content = new Grid
        {
            Margin = new Thickness(24), RowDefinitions = new RowDefinitions("Auto,*,Auto"), RowSpacing = 16,
            Children =
            {
                new StackPanel { Spacing = 3, Children = { new TextBlock { Text = "Payment history", Classes = { "h2" } }, new TextBlock { Text = employee.EmployeeDisplay } } },
                At(table, 1),
                At(new StackPanel { HorizontalAlignment = HorizontalAlignment.Right, Children = { close } }, 2)
            }
        };
    }

    private static T At<T>(T value, int row) where T : Control { Grid.SetRow(value, row); return value; }
}
