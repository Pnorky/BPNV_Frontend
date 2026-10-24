using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using AvaloniaApp.Services;
using AvaloniaApp.ViewModels;
using AvaloniaApp.Views.Dialogs;
using AvaloniaApp.Views.UI;

namespace AvaloniaApp.Views;

public sealed class FuelPricesView : UserControl
{
    public FuelPricesView()
    {
        var table = new PagedTable
        {
            ItemName = "fuel type", ItemNamePlural = "fuel types", PageSize = 10, MinTableWidth = 760, IsSelectable = true
        };
        table.Bind(PagedTable.ItemsSourceProperty, new Binding(nameof(FuelPricesViewModel.Fuels)));
        table.Bind(PagedTable.SelectedItemProperty, new Binding(nameof(FuelPricesViewModel.SelectedFuel)) { Mode = BindingMode.TwoWay });
        table.Bind(PagedTable.IsLoadingProperty, new Binding(nameof(FuelPricesViewModel.IsBusy)));
        table.Bind(PagedTable.ErrorMessageProperty, new Binding(nameof(FuelPricesViewModel.ErrorMessage)));
        table.Bind(PagedTable.IsFilteredProperty, new Binding(nameof(FuelPricesViewModel.IsFiltered)));
        table.Bind(PagedTable.RetryCommandProperty, new Binding(nameof(FuelPricesViewModel.LoadCommand)));
        table.Bind(PagedTable.ClearFiltersCommandProperty, new Binding(nameof(FuelPricesViewModel.ClearFiltersCommand)));
        table.Columns.Add(PagedTableColumn.Create<FuelProductResponse, string>("FUEL TYPE", item => item.Name, new GridLength(1.3, GridUnitType.Star)));
        var price = PagedTableColumn.Create<FuelProductResponse, decimal>("CURRENT PRICE/L", item => item.CurrentPrice, new GridLength(.9, GridUnitType.Star));
        price.HorizontalAlignment = HorizontalAlignment.Right;
        price.CellTemplate = new FuncDataTemplate<FuelProductResponse>((_, _) => Bound(nameof(FuelProductResponse.CurrentPriceDisplay), bold: true), true);
        table.Columns.Add(price);
        var lastChanged = PagedTableColumn.Create<FuelProductResponse, DateTime>("LAST CHANGED", item => item.CurrentPriceEffectiveAtUtc, new GridLength(220));
        lastChanged.CellTemplate = new FuncDataTemplate<FuelProductResponse>((_, _) => Bound(nameof(FuelProductResponse.LastChangedDisplay)), true);
        table.Columns.Add(lastChanged);
        var status = PagedTableColumn.Create<FuelProductResponse, string>("STATUS", item => item.Status, new GridLength(.65, GridUnitType.Star));
        status.CellTemplate = new FuncDataTemplate<FuelProductResponse>((_, _) => Status(nameof(FuelProductResponse.Status)), true);
        table.Columns.Add(status);
        var actions = PagedTableColumn.Create<FuelProductResponse, string>("ACTIONS", _ => "", new GridLength(1, GridUnitType.Star), false);
        actions.CellTemplate = new FuncDataTemplate<FuelProductResponse>((_, _) => FuelActions(), true);
        table.Columns.Add(actions);

        var tabs = new TabControl
        {
            Items =
            {
                new TabItem { Header = "Fuel Details", Content = FuelEditor() },
                new TabItem { Header = "Price History", Content = PriceEditorAndHistory() }
            }
        };

        Content = new Grid
        {
            Margin = new Thickness(30),
            RowDefinitions = new RowDefinitions("Auto,*"),
            RowSpacing = 14,
            Children =
            {
                StatusMessage(),
                At(new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("1.8*,1*"),
                    ColumnSpacing = 16,
                    Children = { Card(table), At(Card(tabs), column: 1) }
                }, row: 1)
            }
        };
    }

    private static Control FuelEditor()
    {
        var name = new TextBox { Classes = { "form-input" }, PlaceholderText = "Example: XCS, Premium, or Diesel" };
        name.Bind(TextBox.TextProperty, new Binding(nameof(FuelPricesViewModel.FuelName)) { Mode = BindingMode.TwoWay });
        var order = new NumberField { Minimum = 0, Maximum = 10000 };
        order.Bind(NumberField.ValueProperty, new Binding(nameof(FuelPricesViewModel.FuelSortOrder)) { Mode = BindingMode.TwoWay });
        var initialPrice = new NumberField { Minimum = 0.01m, Maximum = 999999999999999.99m, Increment = 0.01m, FormatString = "0.00" };
        initialPrice.Bind(NumberField.ValueProperty, new Binding(nameof(FuelPricesViewModel.InitialPrice)) { Mode = BindingMode.TwoWay });
        var initialGroup = new StackPanel { Spacing = 6, Children = { Label("INITIAL PRICE PER LITER"), initialPrice } };
        initialGroup.Bind(Visual.IsVisibleProperty, new Binding(nameof(FuelPricesViewModel.IsNewFuel)));
        var save = Button("", nameof(FuelPricesViewModel.SaveFuelCommand), ActionButtonVariant.Primary);
        save.Bind(ContentControl.ContentProperty, new Binding(nameof(FuelPricesViewModel.FuelSaveText)));

        return new ScrollViewer
        {
            Content = new StackPanel
            {
                Margin = new Thickness(0, 12, 0, 0),
                Spacing = 12,
                Children =
                {
                    Label("FUEL NAME"), name,
                    Label("DISPLAY ORDER"), order,
                    initialGroup,
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal, Spacing = 8,
                        Children = { save, Button("Clear", nameof(FuelPricesViewModel.NewFuelCommand), ActionButtonVariant.Secondary) }
                    }
                }
            }
        };
    }

    private static Control PriceEditorAndHistory()
    {
        var newPrice = new NumberField { Minimum = 0.01m, Maximum = 999999999999999.99m, Increment = 0.01m, FormatString = "0.00" };
        newPrice.Bind(NumberField.ValueProperty, new Binding(nameof(FuelPricesViewModel.NewPrice)) { Mode = BindingMode.TwoWay });
        var change = new ActionButton("Change Price", ActionButtonVariant.Primary, ActionButtonSize.Sm);
        change.Click += async (_, _) =>
        {
            if (GetViewModel(change) is not { SelectedFuel: { } fuel } viewModel ||
                TopLevel.GetTopLevel(change) is not Window owner) return;
            var dialog = new ConfirmDialog();
            dialog.SetConfirmation("Change fuel price",
                $"Change {fuel.Name} from {fuel.CurrentPriceDisplay} to ₱{viewModel.NewPrice:N2}/L? The previous price will remain in history.",
                "Change price");
            await dialog.ShowDialog(owner);
            if (dialog.Confirmed) await viewModel.ChangePriceCommand.ExecuteAsync(null);
        };

        var pricePanel = new StackPanel
        {
            Spacing = 8,
            Children =
            {
                Bound("SelectedFuel.Name", 20, true),
                MutedBound("SelectedFuel.CurrentPriceDisplay"),
                Label("NEW PRICE PER LITER"), newPrice,
                MutedBound(nameof(FuelPricesViewModel.PriceDifferenceDisplay)),
                change
            }
        };
        pricePanel.Bind(Visual.IsVisibleProperty, new Binding(nameof(FuelPricesViewModel.HasSelectedFuel)));

        var history = new ItemsControl();
        history.Bind(ItemsControl.ItemsSourceProperty, new Binding(nameof(FuelPricesViewModel.PriceHistory)));
        history.ItemTemplate = new FuncDataTemplate<FuelPriceResponse>((_, _) => HistoryRow(), true);

        var emptyHistory = new Border
        {
            Padding = new Thickness(18, 28),
            Child = MutedBound(nameof(FuelPricesViewModel.PriceHistoryEmptyMessage))
        };
        emptyHistory.Bind(Visual.IsVisibleProperty, new Binding(nameof(FuelPricesViewModel.HasNoPriceHistory)));

        return new ScrollViewer
        {
            Content = new StackPanel
            {
                Spacing = 16,
                Children =
                {
                    pricePanel,
                    new Separator { Margin = new Thickness(0, 4) },
                    Text("Price history", 16, true),
                    history,
                    emptyHistory
                }
            }
        };
    }

    private static Control HistoryRow()
    {
        var changedBy = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4,
            Children = { Muted("Changed by"), MutedBound(nameof(FuelPriceResponse.CreatedByName)) }
        };
        var details = new StackPanel
        {
            Spacing = 4,
            Children = { Bound(nameof(FuelPriceResponse.EffectiveDisplay), bold: true), changedBy }
        };
        var price = Bound(nameof(FuelPriceResponse.PriceDisplay), 16, true);
        price.HorizontalAlignment = HorizontalAlignment.Right;
        price.VerticalAlignment = VerticalAlignment.Center;

        var row = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            ColumnSpacing = 12,
            Children = { details, At(price, column: 1) }
        };
        var border = new Border
        {
            Padding = new Thickness(10, 12),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Child = row
        };
        border.Bind(Border.BorderBrushProperty, new DynamicResourceExtension("Border"));
        return border;
    }

    private static Control FuelActions()
    {
        var deactivate = new ActionButton("Deactivate", ActionButtonVariant.Danger, ActionButtonSize.Sm);
        deactivate.Click += async (_, _) =>
        {
            if (deactivate.DataContext is not FuelProductResponse fuel || GetViewModel(deactivate) is not { } viewModel ||
                TopLevel.GetTopLevel(deactivate) is not Window owner) return;
            var dialog = new ConfirmDialog();
            dialog.SetConfirmation("Deactivate fuel", $"Deactivate {fuel.Name}? Its price history will be preserved.", "Deactivate");
            await dialog.ShowDialog(owner);
            if (dialog.Confirmed) await viewModel.DeactivateFuelCommand.ExecuteAsync(fuel);
        };
        var reactivate = new ActionButton("Reactivate", ActionButtonVariant.Secondary, ActionButtonSize.Sm);
        reactivate.Click += async (_, _) =>
        {
            if (reactivate.DataContext is FuelProductResponse fuel && GetViewModel(reactivate) is { } viewModel)
                await viewModel.ReactivateFuelCommand.ExecuteAsync(fuel);
        };
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { deactivate, reactivate } };
        void Update()
        {
            if (panel.DataContext is not FuelProductResponse fuel) return;
            deactivate.IsVisible = fuel.IsActive;
            reactivate.IsVisible = !fuel.IsActive;
        }
        panel.DataContextChanged += (_, _) => Update();
        panel.AttachedToVisualTree += (_, _) => Update();
        return panel;
    }

    private static ActionButton Button(string text, string command, ActionButtonVariant variant)
    {
        var button = new ActionButton(text, variant, ActionButtonSize.Sm);
        button.Bind(Avalonia.Controls.Button.CommandProperty, new Binding(command));
        return button;
    }

    private static Border Card(Control child)
    {
        var card = new Border { Padding = new Thickness(18), Child = child };
        card.Classes.Add("theme-card");
        card.Bind(Border.BackgroundProperty, new DynamicResourceExtension("Card"));
        return card;
    }

    private static Border StatusMessage()
    {
        var value = new Border { Padding = new Thickness(12, 8), Child = Bound(nameof(FuelPricesViewModel.StatusMessage)) };
        value.Bind(Border.BackgroundProperty, new DynamicResourceExtension("Secondary"));
        return value;
    }

    private static StatusBadge Status(string path)
    {
        var value = new StatusBadge();
        value.Bind(StatusBadge.StatusProperty, new Binding(path));
        return value;
    }

    private static TextBlock Label(string text) => Text(text, 11, true);
    private static TextBlock Text(string text, double size = 14, bool bold = false) =>
        new() { Text = text, FontSize = size, FontWeight = bold ? Avalonia.Media.FontWeight.SemiBold : Avalonia.Media.FontWeight.Normal };
    private static TextBlock Muted(string text)
    {
        var value = Text(text);
        value.TextWrapping = Avalonia.Media.TextWrapping.Wrap;
        value.Bind(TextBlock.ForegroundProperty, new DynamicResourceExtension("MutedForeground"));
        return value;
    }
    private static TextBlock MutedBound(string path)
    {
        var value = Bound(path);
        value.Bind(TextBlock.ForegroundProperty, new DynamicResourceExtension("MutedForeground"));
        return value;
    }
    private static TextBlock Bound(string path, double size = 14, bool bold = false)
    {
        var value = Text("", size, bold);
        value.Bind(TextBlock.TextProperty, new Binding(path));
        return value;
    }
    private static T At<T>(T value, int row = 0, int column = 0) where T : Control
    {
        Grid.SetRow(value, row);
        Grid.SetColumn(value, column);
        return value;
    }
    private static FuelPricesViewModel? GetViewModel(Control control)
    {
        for (Control? current = control; current is not null; current = current.Parent as Control)
            if (current.DataContext is FuelPricesViewModel viewModel) return viewModel;
        return null;
    }
}
