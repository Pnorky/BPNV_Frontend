using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace AvaloniaApp.Views.Controls;

public class TablePager : UserControl
{
    public TablePager()
    {
        var pageSize = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            VerticalAlignment = VerticalAlignment.Center
        };
        pageSize.Children.Add(Views.ViewCode.Resource(new TextBlock
        {
            Text = "Rows",
            VerticalAlignment = VerticalAlignment.Center
        }, TextBlock.ForegroundProperty, "MutedForeground"));
        pageSize.Children.Add(Views.ViewCode.Bind(Views.ViewCode.Bind(new ComboBox { Width = 64 },
            ComboBox.ItemsSourceProperty, "PageSizeOptions"), ComboBox.SelectedItemProperty, "SelectedPageSize", mode: Avalonia.Data.BindingMode.TwoWay));

        var summaries = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        summaries.Children.Add(Views.ViewCode.Bind(new TextBlock { HorizontalAlignment = HorizontalAlignment.Center },
            TextBlock.TextProperty, "PageSummary"));
        summaries.Children.Add(Views.ViewCode.Resource(Views.ViewCode.Bind(new TextBlock
        {
            FontSize = 11,
            HorizontalAlignment = HorizontalAlignment.Center
        }, TextBlock.TextProperty, "SortSummary"), TextBlock.ForegroundProperty, "MutedForeground"));
        Grid.SetColumn(summaries, 1);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        buttons.Children.Add(Views.ViewCode.Bind(new Button { Content = "Previous", Padding = new Thickness(12, 6) },
            Button.CommandProperty, "PreviousPageCommand"));
        buttons.Children.Add(Views.ViewCode.Bind(new Button { Content = "Next", Padding = new Thickness(12, 6) },
            Button.CommandProperty, "NextPageCommand"));
        Grid.SetColumn(buttons, 2);

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), ColumnSpacing = 16 };
        grid.Children.Add(pageSize);
        grid.Children.Add(summaries);
        grid.Children.Add(buttons);
        Content = Views.ViewCode.Resource(new Border
        {
            Padding = new Thickness(16, 10),
            BorderThickness = new Thickness(0, 1, 0, 0),
            Child = grid
        }, Border.BorderBrushProperty, "Border");
    }
}
