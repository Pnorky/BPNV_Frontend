using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
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
        product.Bind(SearchableSelect.ItemsSourceProperty, new Binding("BodegaBalanceProducts"));
        product.Bind(SearchableSelect.SelectedItemProperty, new Binding("SelectedProduct"));
        product.ItemTemplate = new FuncDataTemplate<ProductResponse>((_, _) =>
            new StackPanel { Children = { Text("Name"), Text("StockDisplay", true) } }, true);
        var search = new IconInput("Search", "Search product, SKU, or supplier...");
        search.Input.Bind(TextBox.TextProperty, new Binding("SearchText"));
        var quantity = new NumberField { Minimum = 1, Maximum = int.MaxValue, Increment = 1, FormatString = "0" };
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
                Status(),
                Field("SEARCH PRODUCTS", search),
                BodegaBalanceSection(),
                Card(form),
                new TextBlock { Text = "Select a product above to view its current Bodega balance before transferring stock." },
                SpoilageSection(),
                history
            }
        };
        Content = new ScrollViewer { Content = root };
    }

    private static Control BodegaBalanceSection()
    {
        var product = new SearchableSelect
        {
            PlaceholderText = "Select product",
            ItemTemplate = new FuncDataTemplate<ProductResponse>((_, _) =>
                new StackPanel { Children = { Text("Name"), Text("StockDisplay", true) } }, true)
        };
        product.Bind(SearchableSelect.ItemsSourceProperty, new Binding("Products"));
        product.Bind(SearchableSelect.SelectedItemProperty, new Binding("BodegaBalanceProduct"));

        var currentBodega = Bound("BodegaCurrentBalanceDisplay");
        currentBodega.FontWeight = FontWeight.SemiBold;
        currentBodega.VerticalAlignment = VerticalAlignment.Center;
        var currentDisplay = Bound("BodegaDisplayBalanceDisplay");
        currentDisplay.FontWeight = FontWeight.SemiBold;
        currentDisplay.VerticalAlignment = VerticalAlignment.Center;
        var actual = new NumberField { Minimum = 1, Maximum = int.MaxValue, Increment = 1, FormatString = "0" };
        actual.Bind(NumberField.ValueProperty, new Binding("BodegaCountedQuantity"));
        var notes = Input("BodegaCountNotes", "Opening balance note");
        var submit = new ActionButton("Set Opening Bodega Balance", ActionButtonVariant.Primary);
        submit.Bind(Button.CommandProperty, new Binding("SetBodegaBalanceCommand"));
        submit.Bind(InputElement.IsEnabledProperty, new Binding("CanSetBodegaBalance"));
        submit.VerticalAlignment = VerticalAlignment.Bottom;

        var fields = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("1.7*,0.7*,0.7*,1*,1.4*,Auto"),
            ColumnSpacing = 12,
            Children =
            {
                Field("PRODUCT", product),
                At(Field("CURRENT BODEGA", currentBodega), 1),
                At(Field("CURRENT DISPLAY", currentDisplay), 2),
                At(Field("ACTUAL BODEGA QUANTITY", actual), 3),
                At(Field("NOTES", notes), 4),
                At(submit, 5)
            }
        };
        var perishableDates = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 12,
            HorizontalAlignment = HorizontalAlignment.Left,
            Children =
            {
                DateOnlyPicker("PRODUCTION DATE (REQUIRED)", "BodegaProductionDate"),
                DateOnlyPicker("EXPIRATION DATE (OPTIONAL)", "BodegaExpirationDate")
            }
        };
        perishableDates.Bind(Visual.IsVisibleProperty, new Binding("ShowPerishableOpeningFields"));

        return Card(new StackPanel
        {
            Spacing = 12,
            Children =
            {
                Heading("Set opening Bodega balance"),
                MutedBound("BodegaBalanceEligibilityText"),
                fields,
                perishableDates
            }
        });
    }

    private static Control SpoilageSection()
    {
        var product = new SearchableSelect
        {
            PlaceholderText = "Select product",
            SearchTextSelector = item => item is ProductResponse value ? $"{value.Name} {value.Sku} {value.SupplierName}" : "",
            ItemTemplate = new FuncDataTemplate<ProductResponse>((value, _) => new TextBlock
            {
                Text = $"{value.Name} | {value.Sku}",
                VerticalAlignment = VerticalAlignment.Center
            }, true)
        };
        product.Bind(SearchableSelect.ItemsSourceProperty, new Binding("Products"));
        product.Bind(SearchableSelect.SelectedItemProperty, new Binding("SpoilageProduct"));
        var unit = new SearchableSelect
        {
            PlaceholderText = "Select unit",
            ItemTemplate = new FuncDataTemplate<ProductUnitResponse>((value, _) => new TextBlock
            {
                Text = $"{value.Label} · {value.PiecesPerUnit:N0} pcs",
                VerticalAlignment = VerticalAlignment.Center
            }, true)
        };
        unit.Bind(SearchableSelect.ItemsSourceProperty, new Binding("SpoilageUnits"));
        unit.Bind(SearchableSelect.SelectedItemProperty, new Binding("SpoilageUnit"));
        var location = new SearchableSelect
        {
            PlaceholderText = "Select location",
            ItemTemplate = new FuncDataTemplate<StockLocationOption>((value, _) => new TextBlock { Text = value.Label }, true)
        };
        location.Bind(SearchableSelect.ItemsSourceProperty, new Binding("StockLocations"));
        location.Bind(SearchableSelect.SelectedItemProperty, new Binding("SpoilageLocation"));
        var lot = new SearchableSelect { PlaceholderText = "Select eligible lot" };
        lot.Bind(SearchableSelect.ItemsSourceProperty, new Binding("SpoilageLots"));
        lot.Bind(SearchableSelect.SelectedItemProperty, new Binding("SpoilageLot"));
        lot.Bind(Visual.IsVisibleProperty, new Binding("SpoilageProductIsPerishable"));
        var reason = new SearchableSelect
        {
            PlaceholderText = "Select reason",
            ItemTemplate = new FuncDataTemplate<ApiSpoilageReason>((value, _) => new TextBlock
            {
                Text = SpoilageReasonLabel(value),
                VerticalAlignment = VerticalAlignment.Center
            }, true)
        };
        reason.Bind(SearchableSelect.ItemsSourceProperty, new Binding("SpoilageReasons"));
        reason.Bind(SearchableSelect.SelectedItemProperty, new Binding("SpoilageReason"));
        var count = new NumberField { Minimum = 1, Maximum = int.MaxValue, Increment = 1, FormatString = "0" };
        count.Bind(NumberField.ValueProperty, new Binding("SpoilageCount"));
        var notes = Input("SpoilageNotes", "Required when reason is Other");
        var submit = new ActionButton("Record spoilage", ActionButtonVariant.Primary);
        submit.Bind(Button.CommandProperty, new Binding("RecordSpoilageCommand"));
        submit.VerticalAlignment = VerticalAlignment.Bottom;

        var fields = new Grid
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            ColumnDefinitions = new ColumnDefinitions("1.55*,1*,1*,1.35*"),
            RowDefinitions = new RowDefinitions("Auto,Auto"),
            RowSpacing = 16,
            ColumnSpacing = 10,
            Children =
            {
                Field("PRODUCT", product),
            }
        };
        var unitField = Field("UNIT", unit); Grid.SetColumn(unitField, 1); fields.Children.Add(unitField);
        var locationField = Field("LOCATION", location); Grid.SetColumn(locationField, 2); fields.Children.Add(locationField);
        var lotField = Field("ELIGIBLE LOT (PERISHABLE)", lot); Grid.SetColumn(lotField, 3); fields.Children.Add(lotField);
        var lowerFields = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("0.55*,1*,1.8*,Auto"),
            ColumnSpacing = 10,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Children = { }
        };
        var countField = Field("COUNT", count); Grid.SetColumn(countField, 0); lowerFields.Children.Add(countField);
        var reasonField = Field("REASON", reason); Grid.SetColumn(reasonField, 1); lowerFields.Children.Add(reasonField);
        var notesField = Field("NOTES", notes); Grid.SetColumn(notesField, 2); lowerFields.Children.Add(notesField);
        Grid.SetColumn(submit, 3); lowerFields.Children.Add(submit);
        Grid.SetColumn(lowerFields, 0); Grid.SetRow(lowerFields, 1); Grid.SetColumnSpan(lowerFields, 4); fields.Children.Add(lowerFields);
        Grid.SetRow(fields, 2);
        var description = Muted("Perishable products require an eligible unexpired lot. The server remains authoritative for stock and lot allocation.");
        Grid.SetRow(description, 1);
        var card = Card(new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,Auto,*"),
            RowSpacing = 12,
            Children =
            {
                Heading("Record spoilage / breakage"),
                description,
                fields
            }
        });
        card.MinHeight = 260;
        return card;
    }

    private static Control HistorySection()
    {
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
        // Keep the full local timestamp visible; the table truncates non-wrapping cells.
        table.Columns.Add(Column("Date & time", item => item.OccurredAtDisplay, 1.8));
        table.Columns.Add(Column("Product", item => item.ProductName, 1.35));
        table.Columns.Add(Column("Movement", item => item.MovementTypeDisplay, 1.35, wrapText: true));
        table.Columns.Add(Column("Quantity", item => item.QuantityDisplay, 0.7, HorizontalAlignment.Right));
        table.Columns.Add(Column("Stock update", item => item.ChangeDisplay, 1.2));
        table.Columns.Add(Column("Balances after", item => item.BalanceDisplay, 1.05));
        table.Columns.Add(Column("Lot / reason", item => item.LotReasonDisplay, 1.05, wrapText: true));
        table.Columns.Add(Column("Reference / notes", item => item.ReferenceNotesDisplay, 1.25, wrapText: true));
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
        return new StackPanel { Spacing = 12, Margin = new Thickness(0, 10, 0, 0), Children = { heading, table } };
    }

    private static TextBox Input(string path, string placeholder) { var value = new TextBox { PlaceholderText = placeholder }; value.Classes.Add("form-input"); value.Bind(TextBox.TextProperty, new Binding(path)); return value; }
    private static StackPanel Field(string label, Control control) { var caption = new TextBlock { Text = label }; caption.Classes.Add("form-label"); return new StackPanel { Spacing = 5, Children = { caption, control } }; }
    private static Border Card(Control child) { var value = new Border { Padding = new Thickness(20), Child = child }; value.Classes.Add("theme-card"); value.Bind(Border.BackgroundProperty, new DynamicResourceExtension("Card")); return value; }
    private static Border Status() { var value = new Border { Padding = new Thickness(12, 8), Child = Bound("StatusMessage") }; value.Bind(Border.BackgroundProperty, new DynamicResourceExtension("Secondary")); return value; }
    private static TextBlock Heading(string text) { var value = new TextBlock { Text = text }; value.Classes.Add("h2"); return value; }
    private static TextBlock Muted(string text) { var value = new TextBlock { Text = text }; value.Bind(TextBlock.ForegroundProperty, new DynamicResourceExtension("MutedForeground")); return value; }
    private static TextBlock MutedBound(string path) { var value = Bound(path); value.TextWrapping = TextWrapping.Wrap; value.Bind(TextBlock.ForegroundProperty, new DynamicResourceExtension("MutedForeground")); return value; }
    private static ShadcnDateTimePicker DateOnlyPicker(string label, string path)
    {
        var value = new ShadcnDateTimePicker { DateLabel = label, ShowTime = false, Width = 250 };
        value.Bind(ShadcnDateTimePicker.SelectedDateProperty, new Binding(path) { Mode = BindingMode.TwoWay });
        return value;
    }
    private static TextBlock Bound(string path) { var value = new TextBlock(); value.Bind(TextBlock.TextProperty, new Binding(path)); return value; }
    private static TextBlock Text(string path, bool muted = false) { var value = Bound(path); if (muted) value.Bind(TextBlock.ForegroundProperty, new DynamicResourceExtension("MutedForeground")); return value; }
    private static string SpoilageReasonLabel(ApiSpoilageReason reason) => reason switch
    {
        ApiSpoilageReason.UnsoldPreparedFood => "Unsold Prepared Food",
        ApiSpoilageReason.PreparationError => "Preparation Error",
        _ => reason.ToString()
    };
    private static T At<T>(T value, int column) where T : Control { Grid.SetColumn(value, column); return value; }
    private static PagedTableColumn Column(
        string header,
        Func<StockMovementResponse, string> selector,
        double width,
        HorizontalAlignment alignment = HorizontalAlignment.Stretch,
        bool wrapText = false)
    {
        var column = PagedTableColumn.Create(header, selector, new GridLength(width, GridUnitType.Star), false);
        column.HorizontalAlignment = alignment;
        column.WrapText = wrapText;
        if (wrapText)
        {
            column.CellTemplate = new FuncDataTemplate<StockMovementResponse>((item, _) => new TextBlock
            {
                Text = selector(item),
                TextWrapping = TextWrapping.Wrap,
                MaxLines = 2,
                HorizontalAlignment = alignment,
                TextAlignment = alignment == HorizontalAlignment.Right ? TextAlignment.Right : TextAlignment.Left
            });
        }
        return column;
    }
}
