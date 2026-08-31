using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using AvaloniaApp.Services;
using AvaloniaApp.ViewModels;
using AvaloniaApp.Views.UI;

namespace AvaloniaApp.Views.Dialogs;

public sealed class PaymentDialog : Window
{
    public PaymentDialog()
    {
        Title = "Checkout Payment - BPNV Convenience Store";
        Width = 500;
        Height = 470;
        MinWidth = 500;
        MinHeight = 470;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        CanResize = false;
        this.BindResource(BackgroundProperty, "Card");
        this.BindResource(ForegroundProperty, "Foreground");

        var title = new TextBlock { Text = "Checkout payment" };
        title.Classes.Add("h2");
        var description = new TextBlock
        {
            Text = "Select how the customer paid, then confirm the transaction.",
            TextWrapping = TextWrapping.Wrap
        };
        description.BindResource(TextBlock.ForegroundProperty, "MutedForeground");

        var total = new TextBlock
        {
            FontSize = 28,
            FontWeight = FontWeight.Bold,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        total.Bind(TextBlock.TextProperty, new Binding(nameof(PaymentDialogViewModel.TotalDisplay)));
        total.BindResource(TextBlock.ForegroundProperty, "Primary");
        var totalCard = new Border
        {
            Padding = new Thickness(16, 12),
            Child = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                Children =
                {
                    new TextBlock
                    {
                        Text = "TOTAL DUE",
                        FontSize = 11,
                        FontWeight = FontWeight.SemiBold,
                        VerticalAlignment = VerticalAlignment.Center
                    },
                    total
                }
            }
        };
        Grid.SetColumn(total, 1);
        totalCard.BindResource(Border.BackgroundProperty, "Card");
        totalCard.BindResource(Border.BorderBrushProperty, "Border");
        totalCard.BorderThickness = new Thickness(1);
        totalCard.CornerRadius = new CornerRadius(0);

        var paymentMethod = new SegmentSwitch(["Cash", "GCash"], 0, selectedIndex =>
        {
            if (DataContext is PaymentDialogViewModel viewModel)
                viewModel.SelectedPaymentMethod = selectedIndex == 0
                    ? ApiPaymentMethod.Cash
                    : ApiPaymentMethod.GCash;
        });

        var amountTendered = new AmountInput
        {
            MinHeight = 42,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        amountTendered.Bind(AmountInput.ValueProperty,
            new Binding(nameof(PaymentDialogViewModel.AmountTendered)) { Mode = BindingMode.TwoWay });

        var change = new TextBlock
        {
            FontSize = 22,
            FontWeight = FontWeight.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        change.Bind(TextBlock.TextProperty, new Binding(nameof(PaymentDialogViewModel.ChangeDisplay)));
        change.BindResource(TextBlock.ForegroundProperty, "SuccessGreen");
        var changeRow = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            Children =
            {
                new TextBlock { Text = "Change", VerticalAlignment = VerticalAlignment.Center },
                change
            }
        };
        Grid.SetColumn(change, 1);

        var cashPanel = new StackPanel
        {
            Spacing = 8,
            Children =
            {
                Label("Amount Tendered"),
                amountTendered,
                changeRow
            }
        };
        cashPanel.Bind(Visual.IsVisibleProperty, new Binding(nameof(PaymentDialogViewModel.IsCash)));

        var gcashConfirmation = new SelectionCheckbox("I confirm the GCash payment was received");
        gcashConfirmation.Bind(ToggleButton.IsCheckedProperty,
            new Binding(nameof(PaymentDialogViewModel.IsGcashConfirmed)) { Mode = BindingMode.TwoWay });
        var gcashPanel = new Border
        {
            Padding = new Thickness(14),
            Child = gcashConfirmation
        };
        gcashPanel.BindResource(Border.BackgroundProperty, "Card");
        gcashPanel.CornerRadius = new CornerRadius(0);
        gcashPanel.Bind(Visual.IsVisibleProperty, new Binding(nameof(PaymentDialogViewModel.IsGCash)));

        var validation = new TextBlock { FontSize = 12, TextWrapping = TextWrapping.Wrap };
        validation.Bind(TextBlock.TextProperty, new Binding(nameof(PaymentDialogViewModel.ValidationMessage)));
        validation.Bind(Visual.IsVisibleProperty, new Binding(nameof(PaymentDialogViewModel.HasValidationError)));
        validation.BindResource(TextBlock.ForegroundProperty, "Destructive");

        var form = new StackPanel
        {
            Spacing = 12,
            Children =
            {
                totalCard,
                Label("Payment Method"),
                paymentMethod,
                cashPanel,
                gcashPanel,
                validation
            }
        };

        var cancel = new Button { Content = "Cancel" };
        cancel.Classes.Add("secondary");
        cancel.Click += (_, _) => Close((PaymentDialogResult?)null);
        var confirm = new Button { Content = "Confirm Payment", IsDefault = true };
        confirm.Classes.Add("primary");
        confirm.Bind(IsEnabledProperty, new Binding(nameof(PaymentDialogViewModel.CanConfirm)));
        confirm.Click += (_, _) =>
        {
            if (DataContext is PaymentDialogViewModel viewModel && viewModel.CreateResult() is { } result)
                Close(result);
        };
        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 10,
            Children = { cancel, confirm }
        };
        Grid.SetRow(actions, 2);

        var content = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*,Auto"),
            RowSpacing = 18,
            Margin = new Thickness(26),
            Children =
            {
                new StackPanel { Spacing = 5, Children = { title, description } },
                form,
                actions
            }
        };
        Grid.SetRow(form, 1);
        var border = new Border { Child = content };
        border.Classes.Add("theme-dialog");
        border.CornerRadius = new CornerRadius(0);
        border.BindResource(Border.BackgroundProperty, "Card");
        Content = border;
    }

    private static TextBlock Label(string text) => new()
    {
        Text = text,
        FontSize = 12,
        FontWeight = FontWeight.SemiBold
    };
}
