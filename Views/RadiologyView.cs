using Avalonia;
using Avalonia.Controls;
using AvaloniaApp.Views.UI;

namespace AvaloniaApp.Views;

public class RadiologyView : UserControl
{
    public RadiologyView()
    {
        var actions = new DockPanel { Margin = new Thickness(0, 0, 0, 20) };
        var button = ViewCode.Bind(new Button { Content = "New Order", Classes = { "primary" } },
            Button.CommandProperty, "NewOrderCommand");
        DockPanel.SetDock(button, Dock.Right);
        actions.Children.Add(button);
        actions.Children.Add(ViewCode.Bind(new TextBox
        {
            PlaceholderText = "Search order, patient, or procedure...",
            Margin = new Thickness(0, 0, 10, 0),
            Classes = { "search" }
        }, TextBox.TextProperty, "SearchText"));
        Grid.SetRow(actions, 1);

        var table = ViewCode.Table("Pager.SourceItems", "radiology order", "radiology orders");
        ViewCode.Bind(table, PagedTable.IsFilteredProperty, "Pager.IsFiltered");
        ViewCode.Bind(table, PagedTable.IsLoadingProperty, "Pager.IsLoading");
        ViewCode.Bind(table, PagedTable.ErrorMessageProperty, "Pager.ErrorMessage");
        ViewCode.Bind(table, PagedTable.IsFirstTimeSetupProperty, "Pager.IsFirstTimeSetup");
        ViewCode.Bind(table, PagedTable.EmptyActionTextProperty, "Pager.StateActionText");
        ViewCode.Bind(table, PagedTable.EmptyActionCommandProperty, "Pager.StateActionCommand");
        ViewCode.AddColumns(table,
            ViewCode.Column("Order #", "OrderNo"),
            ViewCode.Column("Patient", "Patient", 1.3),
            ViewCode.Column("Procedure", "Procedure", 1.3),
            ViewCode.Column("Ordered By", "OrderedBy", 1.2),
            ViewCode.Column("Schedule", "Schedule", 1.1),
            ViewCode.Column("Status", "Status", template: ViewCode.StatusTemplate<RadiologyRecord>("Status")));
        var border = ViewCode.Resource(new Border { CornerRadius = new CornerRadius(8), Child = table },
            Border.BackgroundProperty, "Card");
        Grid.SetRow(border, 2);

        var root = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,*"), Margin = new Thickness(30) };
        root.Children.Add(ViewCode.Heading("Radiology"));
        root.Children.Add(actions);
        root.Children.Add(border);
        Content = root;
    }
}
