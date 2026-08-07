using Avalonia;
using Avalonia.Controls;
using AvaloniaApp.Views.UI;

namespace AvaloniaApp.Views;

public class PatientView : UserControl
{
    public PatientView()
    {
        var actions = new DockPanel { Margin = new Thickness(0, 0, 0, 20) };
        var button = ViewCode.Bind(new Button { Content = "+ New Patient", Classes = { "primary" } },
            Button.CommandProperty, "NewPatientCommand");
        DockPanel.SetDock(button, Dock.Right);
        actions.Children.Add(button);
        actions.Children.Add(ViewCode.Bind(new TextBox
        {
            PlaceholderText = "Search patient by name, ID, or status...",
            Margin = new Thickness(0, 0, 10, 0),
            Classes = { "search" }
        }, TextBox.TextProperty, "SearchText"));
        Grid.SetRow(actions, 1);

        var table = ViewCode.Table("FilteredPatients", "patient", "patients");
        ViewCode.Bind(table, PagedTable.IsFilteredProperty, "HasSearch");
        ViewCode.Bind(table, PagedTable.ClearFiltersCommandProperty, "ClearSearchCommand");
        table.EmptyActionText = "Add Patient";
        ViewCode.Bind(table, PagedTable.EmptyActionCommandProperty, "NewPatientCommand");
        ViewCode.AddColumns(table,
            ViewCode.Column("Patient ID", "PatientId", 1.1),
            ViewCode.Column("Last Name", "LastName", 1.2),
            ViewCode.Column("First Name", "FirstName", 1.1),
            ViewCode.Column("Age", "Age", 0.5),
            ViewCode.Column("Gender", "Gender", 0.8),
            ViewCode.Column("Contact", "Contact", 1.2));
        var border = ViewCode.Resource(new Border
        {
            CornerRadius = new CornerRadius(8),
            ClipToBounds = true,
            Child = table
        }, Border.BackgroundProperty, "Card");
        Grid.SetRow(border, 2);

        var root = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,*"), Margin = new Thickness(30) };
        root.Children.Add(ViewCode.Heading("Patient Management"));
        root.Children.Add(actions);
        root.Children.Add(border);
        Content = root;
    }
}
