using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Layout;
using AvaloniaApp.Services;
using AvaloniaApp.ViewModels;
using AvaloniaApp.Views.UI;

namespace AvaloniaApp.Views.Dialogs;

public sealed class EmployeeDebtPaymentDialog : Window
{
    public EmployeeDebtPaymentDialog()
    {
        Title = "Record Employee Payment - BPNV Convenience Store";
        Width = 500;
        Height = 520;
        MinWidth = 500;
        MinHeight = 520;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        CanResize = false;
        this.BindResource(BackgroundProperty, "Card");
        this.BindResource(ForegroundProperty, "Foreground");

        var employee = Bound(nameof(EmployeeDebtPaymentDialogViewModel.EmployeeDisplay), 18, true);
        var outstanding = Bound(nameof(EmployeeDebtPaymentDialogViewModel.OutstandingDisplay), 26, true);
        outstanding.HorizontalAlignment = HorizontalAlignment.Right;
        outstanding.BindResource(TextBlock.ForegroundProperty, "Primary");
        var amount = new AmountInput { MinHeight = 42 };
        amount.Bind(AmountInput.ValueProperty, new Binding(nameof(EmployeeDebtPaymentDialogViewModel.Amount)) { Mode = BindingMode.TwoWay });
        var method = new SegmentSwitch(["Cash", "GCash"], 0, index =>
        {
            if (DataContext is EmployeeDebtPaymentDialogViewModel vm)
                vm.PaymentMethod = index == 0 ? ApiEmployeeDebtPaymentMethod.Cash : ApiEmployeeDebtPaymentMethod.GCash;
        });
        var reference = new TextBox { PlaceholderText = "Required for GCash", MinHeight = 42 };
        reference.Bind(TextBox.TextProperty, new Binding(nameof(EmployeeDebtPaymentDialogViewModel.ReferenceNumber)) { Mode = BindingMode.TwoWay });
        reference.Bind(Visual.IsVisibleProperty, new Binding(nameof(EmployeeDebtPaymentDialogViewModel.IsGCash)));
        var note = new TextBox { PlaceholderText = "Optional payment note", MinHeight = 70, AcceptsReturn = true, TextWrapping = Avalonia.Media.TextWrapping.Wrap };
        note.Bind(TextBox.TextProperty, new Binding(nameof(EmployeeDebtPaymentDialogViewModel.Note)) { Mode = BindingMode.TwoWay });
        var validation = Bound(nameof(EmployeeDebtPaymentDialogViewModel.ValidationMessage), 12);
        validation.TextWrapping = Avalonia.Media.TextWrapping.Wrap;
        validation.BindResource(TextBlock.ForegroundProperty, "Destructive");

        var cancel = new ActionButton("Cancel", ActionButtonVariant.Secondary);
        cancel.Click += (_, _) => Close((EmployeeDebtPaymentDialogResult?)null);
        var confirm = new ActionButton("Record payment", ActionButtonVariant.Primary) { IsDefault = true };
        confirm.Bind(IsEnabledProperty, new Binding(nameof(EmployeeDebtPaymentDialogViewModel.CanConfirm)));
        confirm.Click += (_, _) =>
        {
            if (DataContext is EmployeeDebtPaymentDialogViewModel vm && vm.CreateResult() is { } result) Close(result);
        };

        Content = new Border
        {
            Classes = { "theme-dialog" },
            Padding = new Thickness(26),
            Child = new Grid
            {
                RowDefinitions = new RowDefinitions("Auto,*,Auto"),
                RowSpacing = 18,
                Children =
                {
                    new StackPanel { Spacing = 4, Children = { new TextBlock { Text = "Record employee payment", Classes = { "h2" } }, employee } },
                    At(new StackPanel { Spacing = 10, Children =
                    {
                        new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = { Label("OUTSTANDING BALANCE"), At(outstanding, column: 1) } },
                        Label("Amount"), amount, Label("Payment method"), method, reference, Label("Note"), note, validation
                    } }, row: 1),
                    At(new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 10, Children = { cancel, confirm } }, row: 2)
                }
            }
        };
    }

    private static TextBlock Bound(string path, double size, bool bold = false) { var value = new TextBlock { FontSize = size, FontWeight = bold ? Avalonia.Media.FontWeight.SemiBold : Avalonia.Media.FontWeight.Normal }; value.Bind(TextBlock.TextProperty, new Binding(path)); return value; }
    private static TextBlock Label(string text) => new() { Text = text, FontSize = 11, FontWeight = Avalonia.Media.FontWeight.SemiBold };
    private static T At<T>(T value, int row = 0, int column = 0) where T : Control { Grid.SetRow(value, row); Grid.SetColumn(value, column); return value; }
}
