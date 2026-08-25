using Avalonia;
using Avalonia.Controls;
using AvaloniaApp.Views.UI;

namespace AvaloniaApp.Views;

public class PharmacyView : UserControl
{
    public PharmacyView()
    {
        Name = "Root";
        var actions = new DockPanel { Margin = new Thickness(0, 0, 0, 20) };
        var button = ViewCode.Bind(new ActionButton("Add Medicine"),
            Button.CommandProperty, "AddMedicineCommand");
        DockPanel.SetDock(button, Dock.Right);
        actions.Children.Add(button);
        var search = new IconInput("Search", "Search medicine by name, code, or category...") { Margin = new Thickness(0, 0, 10, 0) };
        ViewCode.Bind(search.Input, TextBox.TextProperty, "SearchText");
        actions.Children.Add(search);
        Grid.SetRow(actions, 1);

        var table = ViewCode.Table("Pager.SourceItems", "medicine", "medicines");
        ViewCode.Bind(table, PagedTable.IsFilteredProperty, "Pager.IsFiltered");
        ViewCode.Bind(table, PagedTable.IsLoadingProperty, "Pager.IsLoading");
        ViewCode.Bind(table, PagedTable.ErrorMessageProperty, "Pager.ErrorMessage");
        ViewCode.Bind(table, PagedTable.IsFirstTimeSetupProperty, "Pager.IsFirstTimeSetup");
        ViewCode.Bind(table, PagedTable.EmptyActionTextProperty, "Pager.StateActionText");
        ViewCode.Bind(table, PagedTable.EmptyActionCommandProperty, "Pager.StateActionCommand");
        ViewCode.AddColumns(table,
            ViewCode.Column("Code", "MedicineCode", 0.8),
            ViewCode.Column("Medicine Name", "MedicineName", 1.5),
            ViewCode.Column("Category", "Category", 1.1),
            ViewCode.Column("Stock", "Stock", 0.6),
            ViewCode.Column("Unit Price", "UnitPrice", 0.9),
            ViewCode.Column("Expiry Date", "ExpiryDateDisplay"),
            ViewCode.Column("Status", "Status", template: ViewCode.StatusTemplate<MedicineRecord>("Status")));
        var border = ViewCode.Resource(new Border { CornerRadius = new CornerRadius(8), Child = table },
            Border.BackgroundProperty, "Card");
        Grid.SetRow(border, 2);

        var root = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,*"), Margin = new Thickness(30) };
        root.Children.Add(ViewCode.Heading("Pharmacy"));
        root.Children.Add(actions);
        root.Children.Add(border);
        Content = root;
    }
}
