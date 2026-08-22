using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using AvaloniaApp.ViewModels;
using AvaloniaApp.Services;
using AvaloniaApp.Views.UI;

namespace AvaloniaApp.Views;

public sealed class ApiStockMovementsView : UserControl
{
    public ApiStockMovementsView()
    {
        var product = new SearchableSelect { PlaceholderText = "Select product" };
        product.Bind(SearchableSelect.ItemsSourceProperty, new Binding("Products"));
        product.Bind(SearchableSelect.SelectedItemProperty, new Binding("SelectedProduct"));
        product.ItemTemplate = new FuncDataTemplate<ProductResponse>((_, _) =>
            new StackPanel { Children = { Text("Name"), Text("StockDisplay", true) } }, true);
        var search = new IconInput("Search", "Search product, SKU, or supplier...");
        search.Input.Bind(TextBox.TextProperty, new Binding("SearchText"));
        var quantity = new NumberField { Minimum = 1, Increment = 1, FormatString = "0" };
        quantity.Bind(NumberField.ValueProperty, new Binding("Quantity"));
        var reference = Input("Reference", "Optional reference");
        var notes = Input("Notes", "Optional notes");
        var transfer = new ActionButton("Move to Display");
        transfer.Bind(Button.CommandProperty, new Binding("TransferCommand"));
        var form = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("1.8*,1*,1.2*,1.5*,Auto"), ColumnSpacing = 12,
            Children = { Field("PRODUCT", product), At(Field("QUANTITY", quantity), 1), At(Field("REFERENCE", reference), 2), At(Field("NOTES", notes), 3), At(transfer, 4) }
        };
        var history = HistorySection();
        var root = new StackPanel
        {
            Margin = new Thickness(30), Spacing = 14,
            Children =
            {
                new StackPanel { Spacing = 4, Children = { Heading("Stock movements"), Muted("Move API-backed Bodega stock to Display.") } },
                Status(),
                Field("SEARCH PRODUCTS", search),
                Card(form),
                new TextBlock { Text = "Select a product above to view its current Bodega balance before transferring stock." },
                history
            }
        };
        Content = new ScrollViewer { Content = root };
    }

    private static Control HistorySection()
    {
        var search = new IconInput("Search", "Product, SKU, supplier, barcode...");
        search.Input.Bind(TextBox.TextProperty, new Binding("HistorySearchText"));
        var movementType = new SelectDropdown();
        movementType.Bind(SelectDropdown.ItemsSourceProperty, new Binding("MovementTypes"));
        movementType.Bind(SelectDropdown.SelectedItemProperty, new Binding("SelectedMovementType") { Mode = BindingMode.TwoWay });
        var sort = new SelectDropdown();
        sort.Bind(SelectDropdown.ItemsSourceProperty, new Binding("MovementSortOptions"));
        sort.Bind(SelectDropdown.SelectedItemProperty, new Binding("SelectedMovementSort") { Mode = BindingMode.TwoWay });
        var reference = Input("HistoryReference", "Reference or sale number");
        var dateRange = new DateRangePicker
        {
            Width = 490,
            HorizontalAlignment = HorizontalAlignment.Left,
            PlaceholderText = "Pick a date range"
        };
        dateRange.Bind(DateRangePicker.StartDateProperty, new Binding("HistoryFromDate") { Mode = BindingMode.TwoWay });
        dateRange.Bind(DateRangePicker.EndDateProperty, new Binding("HistoryToDate") { Mode = BindingMode.TwoWay });
        var apply = new ActionButton("Apply filters");
        apply.Bind(Button.CommandProperty, new Binding("ApplyHistoryFiltersCommand"));
        var clear = new ActionButton("Clear", ActionButtonVariant.Secondary);
        clear.Bind(Button.CommandProperty, new Binding("ClearHistoryFiltersCommand"));
        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            VerticalAlignment = VerticalAlignment.Bottom,
            Children = { apply, clear }
        };
        var primaryFilters = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("1.5*,1*,1*,1.1*"),
            ColumnSpacing = 10,
            Children =
            {
                Field("SEARCH", search),
                At(Field("MOVEMENT", movementType), 1),
                At(Field("ORDER", sort), 2),
                At(Field("REFERENCE", reference), 3)
            }
        };
        var dateFilters = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("520,*"),
            ColumnSpacing = 10,
            Children =
            {
                Field("DATE RANGE", dateRange),
                At(actions, 1)
            }
        };
        actions.HorizontalAlignment = HorizontalAlignment.Right;
        var filters = new StackPanel { Spacing = 10, Children = { primaryFilters, dateFilters } };

        var table = new PagedTable
        {
            Height = 510,
            MinTableWidth = 1250,
            ItemName = "movement",
            ItemNamePlural = "movements"
        };
        table.Bind(PagedTable.ItemsSourceProperty, new Binding("Movements"));
        table.Bind(PagedTable.PageSizeProperty, new Binding("HistoryPageSize") { Mode = BindingMode.TwoWay });
        table.Bind(PagedTable.ExternalPageProperty, new Binding("HistoryPage") { Mode = BindingMode.TwoWay });
        table.Bind(PagedTable.ExternalTotalCountProperty, new Binding("HistoryTotalCount"));
        table.Bind(PagedTable.ExternalPreviousCommandProperty, new Binding("PreviousHistoryPageCommand"));
        table.Bind(PagedTable.ExternalNextCommandProperty, new Binding("NextHistoryPageCommand"));
        table.Bind(PagedTable.IsLoadingProperty, new Binding("IsHistoryLoading"));
        table.Bind(PagedTable.ErrorMessageProperty, new Binding("HistoryError"));
        table.Bind(PagedTable.IsFilteredProperty, new Binding("IsHistoryFiltered"));
        table.Bind(PagedTable.RetryCommandProperty, new Binding("LoadHistoryCommand"));
        table.Bind(PagedTable.ClearFiltersCommandProperty, new Binding("ClearHistoryFiltersCommand"));
        table.Columns.Add(Column("Date", item => item.OccurredAtDisplay, 1.1));
        table.Columns.Add(Column("Product", item => item.ProductName, 1.35));
        table.Columns.Add(Column("Movement", item => item.MovementTypeDisplay, 1.15));
        table.Columns.Add(Column("Quantity", item => item.QuantityDisplay, 0.7, HorizontalAlignment.Right));
        table.Columns.Add(Column("Stock change", item => item.ChangeDisplay, 1.2));
        table.Columns.Add(Column("Balances after", item => item.BalanceDisplay, 1.05));
        table.Columns.Add(Column("Reference / notes", item => item.ReferenceNotesDisplay, 1.25));
        table.Columns.Add(Column("User", item => item.CreatedByName, 1.25));

        var count = new TextBlock { FontSize = 12 };
        count.Bind(TextBlock.TextProperty, new Binding("HistoryTotalCount") { StringFormat = "{0:N0} recorded movements" });
        count.Bind(TextBlock.ForegroundProperty, new DynamicResourceExtension("MutedForeground"));
        var heading = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            Children =
            {
                new StackPanel
                {
                    Spacing = 4,
                    Children =
                    {
                        Heading("Transaction history"),
                        Muted("All receipts, transfers, sales, opening balances, imports, and inventory adjustments across all users.")
                    }
                },
                At(count, 1)
            }
        };
        count.VerticalAlignment = VerticalAlignment.Bottom;
        return new StackPanel { Spacing = 12, Margin = new Thickness(0, 10, 0, 0), Children = { heading, filters, table } };
    }

    private static TextBox Input(string path, string placeholder) { var value = new TextBox { PlaceholderText = placeholder }; value.Classes.Add("form-input"); value.Bind(TextBox.TextProperty, new Binding(path)); return value; }
    private static StackPanel Field(string label, Control control) { var caption = new TextBlock { Text = label }; caption.Classes.Add("form-label"); return new StackPanel { Spacing = 5, Children = { caption, control } }; }
    private static Border Card(Control child) { var value = new Border { Padding = new Thickness(20), Child = child }; value.Classes.Add("theme-card"); value.Bind(Border.BackgroundProperty, new DynamicResourceExtension("Card")); return value; }
    private static Border Status() { var value = new Border { Padding = new Thickness(12, 8), Child = Bound("StatusMessage") }; value.Bind(Border.BackgroundProperty, new DynamicResourceExtension("Secondary")); return value; }
    private static TextBlock Heading(string text) { var value = new TextBlock { Text = text }; value.Classes.Add("h1"); return value; }
    private static TextBlock Muted(string text) { var value = new TextBlock { Text = text }; value.Bind(TextBlock.ForegroundProperty, new DynamicResourceExtension("MutedForeground")); return value; }
    private static TextBlock Bound(string path) { var value = new TextBlock(); value.Bind(TextBlock.TextProperty, new Binding(path)); return value; }
    private static TextBlock Text(string path, bool muted = false) { var value = Bound(path); if (muted) value.Bind(TextBlock.ForegroundProperty, new DynamicResourceExtension("MutedForeground")); return value; }
    private static T At<T>(T value, int column) where T : Control { Grid.SetColumn(value, column); return value; }
    private static PagedTableColumn Column(
        string header,
        Func<StockMovementResponse, string> selector,
        double width,
        HorizontalAlignment alignment = HorizontalAlignment.Stretch)
    {
        var column = PagedTableColumn.Create(header, selector, new GridLength(width, GridUnitType.Star), false);
        column.HorizontalAlignment = alignment;
        return column;
    }
}
