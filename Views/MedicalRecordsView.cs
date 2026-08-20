using Avalonia;
using Avalonia.Controls;
using AvaloniaApp.Views.UI;

namespace AvaloniaApp.Views;

public class MedicalRecordsView : UserControl
{
    public MedicalRecordsView()
    {
        var actions = new DockPanel { Margin = new Thickness(0, 0, 0, 20) };
        var button = ViewCode.Bind(new ActionButton("New Record Request"),
            Button.CommandProperty, "NewRecordRequestCommand");
        DockPanel.SetDock(button, Dock.Right);
        actions.Children.Add(button);
        var search = new IconInput("Search", "Search MRN or patient name...") { Margin = new Thickness(0, 0, 10, 0) };
        ViewCode.Bind(search.Input, TextBox.TextProperty, "SearchText");
        actions.Children.Add(search);
        Grid.SetRow(actions, 1);

        var table = ViewCode.Table("Pager.SourceItems", "record request", "medical records");
        ViewCode.Bind(table, PagedTable.IsFilteredProperty, "Pager.IsFiltered");
        ViewCode.Bind(table, PagedTable.IsLoadingProperty, "Pager.IsLoading");
        ViewCode.Bind(table, PagedTable.ErrorMessageProperty, "Pager.ErrorMessage");
        ViewCode.Bind(table, PagedTable.IsFirstTimeSetupProperty, "Pager.IsFirstTimeSetup");
        ViewCode.Bind(table, PagedTable.EmptyActionTextProperty, "Pager.StateActionText");
        ViewCode.Bind(table, PagedTable.EmptyActionCommandProperty, "Pager.StateActionCommand");
        ViewCode.AddColumns(table,
            ViewCode.Column("MRN", "MRN"),
            ViewCode.Column("Patient Name", "PatientName", 1.5),
            ViewCode.Column("Last Visit", "LastVisit"),
            ViewCode.Column("Record Status", "RecordStatus", template: ViewCode.StatusTemplate<MedicalRecord>("RecordStatus")),
            ViewCode.Column("Chart", "ChartComplete", template: ViewCode.StatusTemplate<MedicalRecord>("ChartComplete")),
            ViewCode.Column("Location", "Location"));
        var border = ViewCode.Resource(new Border { CornerRadius = new CornerRadius(8), Child = table },
            Border.BackgroundProperty, "Card");
        Grid.SetRow(border, 2);

        var root = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,*"), Margin = new Thickness(30) };
        root.Children.Add(ViewCode.Heading("Medical Records"));
        root.Children.Add(actions);
        root.Children.Add(border);
        Content = root;
    }
}
