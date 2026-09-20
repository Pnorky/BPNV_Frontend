using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using AvaloniaApp.Views.UI;

namespace AvaloniaApp.Views.Dialogs;

public sealed class OpeningFloatDialog : Window
{
    public OpeningFloatDialog()
    {
        Title = "Opening Cash Float - BPNV Convenience Store";
        Width = 440;
        Height = 320;
        MinWidth = 440;
        MinHeight = 320;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        this.BindResource(BackgroundProperty, "Card");
        this.BindResource(ForegroundProperty, "Foreground");

        var amount = new AmountInput { Value = 0m, MinHeight = 42 };
        var cancel = new Button { Content = "Cancel" };
        cancel.Classes.Add("secondary");
        cancel.Click += (_, _) => Close((decimal?)null);
        var confirm = new Button { Content = "Clock in", IsDefault = true };
        confirm.Classes.Add("primary");
        confirm.Click += (_, _) => Close(amount.Value ?? 0m);
        confirm.IsEnabled = false;
        var received = new SelectionCheckbox("I counted and received this opening cash for change");
        received.IsCheckedChanged += (_, _) => confirm.IsEnabled = received.IsChecked == true;
        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 10,
            Children = { cancel, confirm }
        };

        var title = new TextBlock { Text = "Confirm opening cash", FontSize = 20, FontWeight = Avalonia.Media.FontWeight.SemiBold };
        var description = new TextBlock
        {
            Text = "Count the terminal cash before starting sales. This amount is included in the shift cash difference review.",
            TextWrapping = Avalonia.Media.TextWrapping.Wrap
        };
        description.BindResource(TextBlock.ForegroundProperty, "MutedForeground");
        var label = new TextBlock { Text = "OPENING CASH FLOAT", FontSize = 11, FontWeight = Avalonia.Media.FontWeight.SemiBold };

        var content = new StackPanel
        {
            Margin = new Thickness(26),
            Spacing = 12,
            Children = { title, description, label, amount, received, actions }
        };
        var border = new Border { Child = content };
        border.Classes.Add("theme-dialog");
        border.BindResource(Border.BackgroundProperty, "Card");
        Content = border;
    }
}
