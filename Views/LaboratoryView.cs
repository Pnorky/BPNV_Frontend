using Avalonia;
using Avalonia.Controls;
using AvaloniaApp.Views.UI;

namespace AvaloniaApp.Views;

public class LaboratoryView : UserControl
{
    public LaboratoryView()
    {
        var pending = ViewCode.Table("PendingPager.SourceItems", "laboratory order", "pending laboratory orders");
        ViewCode.Bind(pending, PagedTable.IsLoadingProperty, "PendingPager.IsLoading");
        ViewCode.Bind(pending, PagedTable.ErrorMessageProperty, "PendingPager.ErrorMessage");
        ViewCode.Bind(pending, PagedTable.IsFirstTimeSetupProperty, "PendingPager.IsFirstTimeSetup");
        ViewCode.Bind(pending, PagedTable.EmptyActionTextProperty, "PendingPager.StateActionText");
        ViewCode.Bind(pending, PagedTable.EmptyActionCommandProperty, "PendingPager.StateActionCommand");
        ViewCode.AddColumns(pending,
            ViewCode.Column("Order #", "OrderNo"),
            ViewCode.Column("Patient", "Patient", 1.3),
            ViewCode.Column("Test Type", "TestType", 1.3),
            ViewCode.Column("Ordered By", "OrderedBy", 1.2),
            ViewCode.Column("Date Ordered", "DateOrderedDisplay", 1.1),
            ViewCode.Column("Priority", "Priority", template: ViewCode.StatusTemplate<LabOrderRecord>("Priority")));

        var completed = ViewCode.Table("CompletedPager.SourceItems", "laboratory result", "completed laboratory results");
        ViewCode.Bind(completed, PagedTable.IsLoadingProperty, "CompletedPager.IsLoading");
        ViewCode.Bind(completed, PagedTable.ErrorMessageProperty, "CompletedPager.ErrorMessage");
        ViewCode.Bind(completed, PagedTable.IsFirstTimeSetupProperty, "CompletedPager.IsFirstTimeSetup");
        ViewCode.Bind(completed, PagedTable.EmptyActionTextProperty, "CompletedPager.StateActionText");
        ViewCode.Bind(completed, PagedTable.EmptyActionCommandProperty, "CompletedPager.StateActionCommand");
        ViewCode.AddColumns(completed,
            ViewCode.Column("Order #", "OrderNo"),
            ViewCode.Column("Patient", "Patient", 1.3),
            ViewCode.Column("Test Type", "TestType", 1.3),
            ViewCode.Column("Result", "Result", 1.2),
            ViewCode.Column("Completed", "CompletedDisplay", 1.1));

        var tabs = new TabControl();
        tabs.Items.Add(new TabItem { Header = "Pending Orders", Content = pending });
        tabs.Items.Add(new TabItem { Header = "Completed Results", Content = completed });
        Grid.SetRow(tabs, 1);
        var root = new Grid { RowDefinitions = new RowDefinitions("Auto,*"), Margin = new Thickness(30) };
        root.Children.Add(ViewCode.Heading("Laboratory"));
        root.Children.Add(tabs);
        Content = root;
    }
}
