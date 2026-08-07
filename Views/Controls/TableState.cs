using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using AvaloniaApp.Converters;
using Lucide.Avalonia;

namespace AvaloniaApp.Views.Controls;

public class TableState : UserControl
{
    public TableState()
    {
        Views.ViewCode.Resource(this, BackgroundProperty, "Card");
        Views.ViewCode.Bind(this, IsVisibleProperty, "ShowState");

        var icon = Views.ViewCode.Bind(new LucideIcon
        {
            Width = 26,
            Height = 26,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        }, LucideIcon.KindProperty, "StateIcon", new StringToIconConverter());
        Views.ViewCode.Bind(icon, IsVisibleProperty, "ShowStateIcon");
        Views.ViewCode.Resource(icon, ForegroundProperty, "Primary");
        var progress = Views.ViewCode.Bind(new ProgressBar
        {
            Width = 30,
            Height = 4,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            IsIndeterminate = true
        }, IsVisibleProperty, "IsLoading");
        var iconGrid = new Grid();
        iconGrid.Children.Add(icon);
        iconGrid.Children.Add(progress);
        var iconBorder = Views.ViewCode.Resource(new Border
        {
            Width = 56,
            Height = 56,
            CornerRadius = new CornerRadius(28),
            HorizontalAlignment = HorizontalAlignment.Center,
            Child = iconGrid
        }, Border.BackgroundProperty, "Secondary");

        var state = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MaxWidth = 420,
            Spacing = 10,
            Margin = new Thickness(32)
        };
        state.Children.Add(iconBorder);
        var title = Views.ViewCode.Resource(Views.ViewCode.Bind(new TextBlock
        {
            TextAlignment = Avalonia.Media.TextAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            Classes = { "h2" }
        }, TextBlock.TextProperty, "StateTitle"), TextBlock.ForegroundProperty, "Foreground");
        state.Children.Add(title);
        state.Children.Add(Views.ViewCode.Resource(Views.ViewCode.Bind(new TextBlock
        {
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            TextAlignment = Avalonia.Media.TextAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center
        }, TextBlock.TextProperty, "StateMessage"), TextBlock.ForegroundProperty, "MutedForeground"));
        var action = Views.ViewCode.Bind(Views.ViewCode.Bind(new Button
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 6, 0, 0),
            Classes = { "primary" }
        }, Button.ContentProperty, "StateActionText"), Button.CommandProperty, "StateActionCommand");
        Views.ViewCode.Bind(action, IsVisibleProperty, "HasStateAction");
        state.Children.Add(action);
        Content = new Grid { MinHeight = 260, Children = { state } };
    }
}
