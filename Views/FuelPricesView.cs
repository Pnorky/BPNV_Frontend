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
        var search = new TextBox { Classes = { "search" }, PlaceholderText = "Search fuel name", MinWidth = 220 };
        search.Bind(TextBox.TextProperty, new Binding(nameof(FuelPricesViewModel.SearchText)) { Mode = BindingMode.TwoWay });
        var categoryFilter = new ComboBox { Classes = { "form-select" }, PlaceholderText = "All categories", MinWidth = 170 };
        categoryFilter.Bind(ItemsControl.ItemsSourceProperty, new Binding(nameof(FuelPricesViewModel.Categories)));
        categoryFilter.Bind(SelectingItemsControl.SelectedItemProperty, new Binding(nameof(FuelPricesViewModel.FilterCategory)) { Mode = BindingMode.TwoWay });
        var includeInactive = new CheckBox { Content = "Include inactive", VerticalAlignment = VerticalAlignment.Center };
        includeInactive.Bind(ToggleButton.IsCheckedProperty, new Binding(nameof(FuelPricesViewModel.IncludeInactive)) { Mode = BindingMode.TwoWay });

        var table = new PagedTable
        {
            ItemName = "fuel", ItemNamePlural = "fuels", PageSize = 10, MinTableWidth = 780, IsSelectable = true
        };
        table.Bind(PagedTable.ItemsSourceProperty, new Binding(nameof(FuelPricesViewModel.Fuels)));
        table.Bind(PagedTable.SelectedItemProperty, new Binding(nameof(FuelPricesViewModel.SelectedFuel)) { Mode = BindingMode.TwoWay });
        table.Bind(PagedTable.IsLoadingProperty, new Binding(nameof(FuelPricesViewModel.IsBusy)));
        table.Bind(PagedTable.ErrorMessageProperty, new Binding(nameof(FuelPricesViewModel.ErrorMessage)));
        table.Bind(PagedTable.IsFilteredProperty, new Binding(nameof(FuelPricesViewModel.IsFiltered)));
        table.Bind(PagedTable.RetryCommandProperty, new Binding(nameof(FuelPricesViewModel.LoadCommand)));
        table.Bind(PagedTable.ClearFiltersCommandProperty, new Binding(nameof(FuelPricesViewModel.ClearFiltersCommand)));
        table.Columns.Add(PagedTableColumn.Create<FuelProductResponse, string>("FUEL", item => item.Name, new GridLength(1.3, GridUnitType.Star)));
        table.Columns.Add(PagedTableColumn.Create<FuelProductResponse, string>("CATEGORY", item => item.CategoryName, new GridLength(1, GridUnitType.Star)));
        var price = PagedTableColumn.Create<FuelProductResponse, decimal>("CURRENT PRICE/L", item => item.CurrentPrice, new GridLength(.9, GridUnitType.Star));
        price.HorizontalAlignment = HorizontalAlignment.Right;
        price.CellTemplate = new FuncDataTemplate<FuelProductResponse>((_, _) => Bound(nameof(FuelProductResponse.CurrentPriceDisplay), bold: true), true);
        table.Columns.Add(price);
        var lastChanged = PagedTableColumn.Create<FuelProductResponse, DateTime>("LAST CHANGED", item => item.CurrentPriceEffectiveAtUtc, new GridLength(1.1, GridUnitType.Star));
        lastChanged.CellTemplate = new FuncDataTemplate<FuelProductResponse>((_, _) => Bound(nameof(FuelProductResponse.LastChangedDisplay)), true);
        table.Columns.Add(lastChanged);
        var status = PagedTableColumn.Create<FuelProductResponse, string>("STATUS", item => item.Status, new GridLength(.65, GridUnitType.Star));
        status.CellTemplate = new FuncDataTemplate<FuelProductResponse>((_, _) => Status(nameof(FuelProductResponse.Status)), true);
        table.Columns.Add(status);
        var actions = PagedTableColumn.Create<FuelProductResponse, string>("ACTIONS", _ => "", new GridLength(1, GridUnitType.Star), false);
        actions.CellTemplate = new FuncDataTemplate<FuelProductResponse>((_, _) => FuelActions(), true);
        table.Columns.Add(actions);

        var filters = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            Children =
            {
                search, categoryFilter, includeInactive,
                Button("Search", nameof(FuelPricesViewModel.LoadFuelsCommand), ActionButtonVariant.Primary),
                Button("Clear", nameof(FuelPricesViewModel.ClearFiltersCommand), ActionButtonVariant.Secondary),
                Button("Add Fuel", nameof(FuelPricesViewModel.NewFuelCommand), ActionButtonVariant.Primary)
            }
        };

        var tabs = new TabControl
        {
            Items =
            {
                new TabItem { Header = "Fuel Details", Content = FuelEditor() },
                new TabItem { Header = "Price History", Content = PriceEditorAndHistory() },
                new TabItem { Header = "Categories", Content = CategoryEditor() }
            }
        };

        Content = new Grid
        {
            Margin = new Thickness(30),
            RowDefinitions = new RowDefinitions("Auto,Auto,*"),
            RowSpacing = 14,
            Children =
            {
                filters,
                At(StatusMessage(), row: 1),
                At(new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("1.8*,1*"),
                    ColumnSpacing = 16,
                    Children = { Card(table), At(Card(tabs), column: 1) }
                }, row: 2)
            }
        };
    }

    private static Control FuelEditor()
    {
        var name = new TextBox { Classes = { "form-input" }, PlaceholderText = "Example: Petron XCS" };
        name.Bind(TextBox.TextProperty, new Binding(nameof(FuelPricesViewModel.FuelName)) { Mode = BindingMode.TwoWay });
        var category = new ComboBox { Classes = { "form-select" }, PlaceholderText = "Select category" };
        category.Bind(ItemsControl.ItemsSourceProperty, new Binding(nameof(FuelPricesViewModel.ActiveCategories)));
        category.Bind(SelectingItemsControl.SelectedItemProperty, new Binding(nameof(FuelPricesViewModel.FuelCategory)) { Mode = BindingMode.TwoWay });
        var order = new NumberField { Minimum = 0, Maximum = 10000 };
        order.Bind(NumberField.ValueProperty, new Binding(nameof(FuelPricesViewModel.FuelSortOrder)) { Mode = BindingMode.TwoWay });
        var initialPrice = new NumberField { Minimum = 0.0001m, Maximum = 99999999999999.9999m, FormatString = "N4" };
        initialPrice.Bind(NumberField.ValueProperty, new Binding(nameof(FuelPricesViewModel.InitialPrice)) { Mode = BindingMode.TwoWay });
        var initialGroup = new StackPanel { Spacing = 6, Children = { Label("INITIAL PRICE PER LITER"), initialPrice } };
        initialGroup.Bind(Visual.IsVisibleProperty, new Binding(nameof(FuelPricesViewModel.IsNewFuel)));
        var save = Button("", nameof(FuelPricesViewModel.SaveFuelCommand), ActionButtonVariant.Primary);
        save.Bind(ContentControl.ContentProperty, new Binding(nameof(FuelPricesViewModel.FuelSaveText)));

        return new ScrollViewer
        {
            Content = new StackPanel
            {
                Spacing = 12,
                Children =
                {
                    Bound(nameof(FuelPricesViewModel.FuelEditorTitle), 20, true),
                    Muted("Select a row to edit it, or add a new fuel with its initial pump price."),
                    Label("FUEL NAME"), name,
                    Label("CATEGORY"), category,
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
        var newPrice = new NumberField { Minimum = 0.0001m, Maximum = 99999999999999.9999m, FormatString = "N4" };
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

        var history = new PagedTable { ItemName = "price", ItemNamePlural = "prices", PageSize = 5, MinTableWidth = 520 };
        history.Bind(PagedTable.ItemsSourceProperty, new Binding(nameof(FuelPricesViewModel.PriceHistory)));
        var effective = PagedTableColumn.Create<FuelPriceResponse, DateTime>("EFFECTIVE", item => item.EffectiveAtUtc, new GridLength(1.2, GridUnitType.Star));
        effective.CellTemplate = new FuncDataTemplate<FuelPriceResponse>((_, _) => Bound(nameof(FuelPriceResponse.EffectiveDisplay)), true);
        history.Columns.Add(effective);
        var historyPrice = PagedTableColumn.Create<FuelPriceResponse, decimal>("PRICE/L", item => item.PricePerLiter, new GridLength(.8, GridUnitType.Star));
        historyPrice.HorizontalAlignment = HorizontalAlignment.Right;
        historyPrice.CellTemplate = new FuncDataTemplate<FuelPriceResponse>((_, _) => Bound(nameof(FuelPriceResponse.PriceDisplay), bold: true), true);
        history.Columns.Add(historyPrice);
        history.Columns.Add(PagedTableColumn.Create<FuelPriceResponse, string>("CHANGED BY", item => item.CreatedByName, new GridLength(1, GridUnitType.Star)));

        return new ScrollViewer
        {
            Content = new StackPanel
            {
                Spacing = 16,
                Children = { pricePanel, Separator(), BoundText("Price history", 16, true), history }
            }
        };
    }

    private static Control CategoryEditor()
    {
        var categories = new PagedTable
        {
            ItemName = "category", ItemNamePlural = "categories", PageSize = 5, MinTableWidth = 520, IsSelectable = true
        };
        categories.Bind(PagedTable.ItemsSourceProperty, new Binding(nameof(FuelPricesViewModel.Categories)));
        categories.Bind(PagedTable.SelectedItemProperty, new Binding(nameof(FuelPricesViewModel.SelectedCategory)) { Mode = BindingMode.TwoWay });
        categories.Columns.Add(PagedTableColumn.Create<FuelCategoryResponse, string>("CATEGORY", item => item.Name, new GridLength(1.2, GridUnitType.Star)));
        categories.Columns.Add(PagedTableColumn.Create<FuelCategoryResponse, int>("ORDER", item => item.SortOrder, new GridLength(.5, GridUnitType.Star)));
        var status = PagedTableColumn.Create<FuelCategoryResponse, string>("STATUS", item => item.Status, new GridLength(.7, GridUnitType.Star));
        status.CellTemplate = new FuncDataTemplate<FuelCategoryResponse>((_, _) => Status(nameof(FuelCategoryResponse.Status)), true);
        categories.Columns.Add(status);
        var actions = PagedTableColumn.Create<FuelCategoryResponse, string>("ACTIONS", _ => "", new GridLength(1, GridUnitType.Star), false);
        actions.CellTemplate = new FuncDataTemplate<FuelCategoryResponse>((_, _) => CategoryActions(), true);
        categories.Columns.Add(actions);

        var name = new TextBox { Classes = { "form-input" }, PlaceholderText = "Example: Gasoline" };
        name.Bind(TextBox.TextProperty, new Binding(nameof(FuelPricesViewModel.CategoryName)) { Mode = BindingMode.TwoWay });
        var order = new NumberField { Minimum = 0, Maximum = 10000 };
        order.Bind(NumberField.ValueProperty, new Binding(nameof(FuelPricesViewModel.CategorySortOrder)) { Mode = BindingMode.TwoWay });
        var save = Button("", nameof(FuelPricesViewModel.SaveCategoryCommand), ActionButtonVariant.Primary);
        save.Bind(ContentControl.ContentProperty, new Binding(nameof(FuelPricesViewModel.CategorySaveText)));

        return new ScrollViewer
        {
            Content = new StackPanel
            {
                Spacing = 12,
                Children =
                {
                    categories,
                    Separator(),
                    Bound(nameof(FuelPricesViewModel.CategoryEditorTitle), 20, true),
                    Label("CATEGORY NAME"), name,
                    Label("DISPLAY ORDER"), order,
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal, Spacing = 8,
                        Children = { save, Button("Clear", nameof(FuelPricesViewModel.NewCategoryCommand), ActionButtonVariant.Secondary) }
                    }
                }
            }
        };
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
        return ActiveActions(deactivate, reactivate, item => ((FuelProductResponse)item).IsActive);
    }

    private static Control CategoryActions()
    {
        var deactivate = new ActionButton("Deactivate", ActionButtonVariant.Danger, ActionButtonSize.Sm);
        deactivate.Click += async (_, _) =>
        {
            if (deactivate.DataContext is not FuelCategoryResponse category || GetViewModel(deactivate) is not { } viewModel ||
                TopLevel.GetTopLevel(deactivate) is not Window owner) return;
            var dialog = new ConfirmDialog();
            dialog.SetConfirmation("Deactivate category", $"Deactivate {category.Name}? Active fuels must be reassigned or deactivated first.", "Deactivate");
            await dialog.ShowDialog(owner);
            if (dialog.Confirmed) await viewModel.DeactivateCategoryCommand.ExecuteAsync(category);
        };
        var reactivate = new ActionButton("Reactivate", ActionButtonVariant.Secondary, ActionButtonSize.Sm);
        reactivate.Click += async (_, _) =>
        {
            if (reactivate.DataContext is FuelCategoryResponse category && GetViewModel(reactivate) is { } viewModel)
                await viewModel.ReactivateCategoryCommand.ExecuteAsync(category);
        };
        return ActiveActions(deactivate, reactivate, item => ((FuelCategoryResponse)item).IsActive);
    }

    private static Control ActiveActions(Control deactivate, Control reactivate, Func<object, bool> isActive)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { deactivate, reactivate } };
        void Update()
        {
            if (panel.DataContext is null) return;
            var active = isActive(panel.DataContext);
            deactivate.IsVisible = active;
            reactivate.IsVisible = !active;
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

    private static Separator Separator() => new() { Margin = new Thickness(0, 4) };
    private static TextBlock Label(string text) => new() { Text = text, FontSize = 11, FontWeight = Avalonia.Media.FontWeight.SemiBold };
    private static TextBlock Muted(string text)
    {
        var value = new TextBlock { Text = text, TextWrapping = Avalonia.Media.TextWrapping.Wrap };
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
        var value = new TextBlock { FontSize = size, FontWeight = bold ? Avalonia.Media.FontWeight.SemiBold : Avalonia.Media.FontWeight.Normal };
        value.Bind(TextBlock.TextProperty, new Binding(path));
        return value;
    }
    private static TextBlock BoundText(string text, double size = 14, bool bold = false) =>
        new() { Text = text, FontSize = size, FontWeight = bold ? Avalonia.Media.FontWeight.SemiBold : Avalonia.Media.FontWeight.Normal };
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
