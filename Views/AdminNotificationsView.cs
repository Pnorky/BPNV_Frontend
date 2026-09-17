using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using Avalonia.VisualTree;
using AvaloniaApp.Services;
using AvaloniaApp.ViewModels;
using AvaloniaApp.Views.UI;

namespace AvaloniaApp.Views;

public sealed class AdminNotificationsView : UserControl
{
    public AdminNotificationsView()
    {
        var refresh = new ActionButton("Refresh", ActionButtonVariant.Secondary);
        refresh.Bind(Button.CommandProperty, new Binding("RefreshCommand"));

        var markAll = new ActionButton("Mark all read", ActionButtonVariant.Primary);
        markAll.Bind(Button.CommandProperty, new Binding("MarkAllReadCommand"));

        var loading = new ProgressBar { Height = 4, IsIndeterminate = true };
        loading.Bind(Visual.IsVisibleProperty, new Binding("IsLoading"));

        var error = new Border
        {
            BorderThickness = new Thickness(3, 0, 0, 0),
            Padding = new Thickness(12, 9),
            CornerRadius = new CornerRadius(7),
            Child = Bound("ErrorMessage", FontWeight.SemiBold, wrap: true)
        };
        Resource(error, Border.BackgroundProperty, "Secondary");
        Resource(error, Border.BorderBrushProperty, "Destructive");
        error.Bind(Visual.IsVisibleProperty, new Binding("HasError"));

        var empty = Card(new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            Spacing = 5,
            Children =
            {
                Heading("No notifications", "h2"),
                Muted("New shift events and remittance reviews will appear here.")
            }
        }, new Thickness(24));
        empty.Bind(Visual.IsVisibleProperty, new Binding("ShowEmptyState"));

        var notifications = new ItemsControl
        {
            ItemTemplate = new FuncDataTemplate<AdminNotificationResponse>((notification, _) => NotificationCard(notification), true)
        };
        notifications.Bind(ItemsControl.ItemsSourceProperty, new Binding("Items"));

        Content = new Grid
        {
            Margin = new Thickness(30),
            RowDefinitions = new RowDefinitions("Auto,Auto,Auto,*"),
            RowSpacing = 14,
            Children =
            {
                new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                    ColumnSpacing = 16,
                    Children =
                    {
                        new StackPanel
                        {
                            Spacing = 4,
                            Children =
                            {
                                Heading("Admin notifications", "h1"),
                                Muted("Persisted cashier shift events and remittance follow-up.")
                            }
                        },
                        At(new StackPanel
                        {
                            Orientation = Orientation.Horizontal,
                            VerticalAlignment = VerticalAlignment.Center,
                            Spacing = 8,
                            Children = { refresh, markAll }
                        }, column: 1)
                    }
                },
                At(new StackPanel { Spacing = 8, Children = { loading, error } }, row: 1),
                At(Summary(), row: 2),
                At(new ScrollViewer
                {
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                    Content = new Grid { Children = { empty, notifications } }
                }, row: 3)
            }
        };
    }

    private static Border Summary()
    {
        var summary = new Border
        {
            Padding = new Thickness(12, 8),
            CornerRadius = new CornerRadius(7),
            Child = Bound("InboxSummary", FontWeight.SemiBold)
        };
        Resource(summary, Border.BackgroundProperty, "Secondary");
        return summary;
    }

    private static Control NotificationCard(AdminNotificationResponse notification)
    {
        var markRead = new ActionButton("Mark read", ActionButtonVariant.Secondary, ActionButtonSize.Sm)
        {
            IsVisible = !notification.IsRead
        };
        markRead.Click += (_, _) => GetViewModel(markRead)?.MarkReadCommand.Execute(notification);

        var review = new ActionButton("Review shift", ActionButtonVariant.Primary, ActionButtonSize.Sm)
        {
            IsVisible = notification.ShiftSessionId is not null
        };
        review.Click += (_, _) => GetViewModel(review)?.OpenLinkedSessionCommand.Execute(notification);

        var status = new Border
        {
            Padding = new Thickness(9, 4),
            CornerRadius = new CornerRadius(12),
            Child = new TextBlock
            {
                Text = notification.Status,
                FontSize = 11,
                FontWeight = FontWeight.SemiBold
            }
        };
        Resource(status, Border.BackgroundProperty, notification.IsRead ? "Muted" : "Secondary");

        var type = new TextBlock { Text = notification.Type, FontSize = 11, FontWeight = FontWeight.SemiBold };
        Resource(type, TextBlock.ForegroundProperty, "MutedForeground");

        var message = new TextBlock { Text = notification.Message, TextWrapping = TextWrapping.Wrap };
        var created = Muted(notification.CreatedAtDisplay, 11);

        var card = Card(new StackPanel
        {
            Spacing = 10,
            Children =
            {
                new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                    ColumnSpacing = 12,
                    Children =
                    {
                        new StackPanel
                        {
                            Spacing = 3,
                            Children =
                            {
                                new TextBlock
                                {
                                    Text = notification.Title,
                                    FontSize = 16,
                                    FontWeight = notification.IsRead ? FontWeight.SemiBold : FontWeight.Bold,
                                    TextWrapping = TextWrapping.Wrap
                                },
                                type
                            }
                        },
                        At(status, column: 1)
                    }
                },
                message,
                new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                    ColumnSpacing = 12,
                    Children =
                    {
                        created,
                        At(new StackPanel
                        {
                            Orientation = Orientation.Horizontal,
                            Spacing = 8,
                            Children = { markRead, review }
                        }, column: 1)
                    }
                }
            }
        }, new Thickness(16));
        card.Margin = new Thickness(0, 0, 0, 10);
        card.BorderThickness = notification.IsRead ? new Thickness(1) : new Thickness(4, 1, 1, 1);
        Resource(card, Border.BorderBrushProperty, notification.IsRead ? "Border" : "Primary");
        return card;
    }

    private static AdminNotificationsViewModel? GetViewModel(Control control) =>
        control.GetVisualAncestors().OfType<AdminNotificationsView>().FirstOrDefault()?.DataContext as AdminNotificationsViewModel;

    private static Border Card(Control child, Thickness padding)
    {
        var card = new Border { Padding = padding, Child = child, Classes = { "theme-card" } };
        Resource(card, Border.BackgroundProperty, "Card");
        return card;
    }

    private static TextBlock Heading(string text, string styleClass) =>
        new() { Text = text, Classes = { styleClass } };

    private static TextBlock Bound(string path, FontWeight? weight = null, bool wrap = false)
    {
        var text = new TextBlock
        {
            FontWeight = weight ?? FontWeight.Normal,
            TextWrapping = wrap ? TextWrapping.Wrap : TextWrapping.NoWrap
        };
        text.Bind(TextBlock.TextProperty, new Binding(path));
        return text;
    }

    private static TextBlock Muted(string text, double? size = null)
    {
        var value = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap };
        if (size is not null) value.FontSize = size.Value;
        Resource(value, TextBlock.ForegroundProperty, "MutedForeground");
        return value;
    }

    private static T At<T>(T control, int row = 0, int column = 0) where T : Control
    {
        Grid.SetRow(control, row);
        Grid.SetColumn(control, column);
        return control;
    }

    private static void Resource(AvaloniaObject target, AvaloniaProperty property, object key) =>
        target.Bind(property, new DynamicResourceExtension(key));
}
