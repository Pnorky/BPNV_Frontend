using System.Collections;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using AvaloniaApp.Views.Controls;

namespace AvaloniaApp.Views.UI;

public sealed class SelectDropdown : Grid
{
    private const double ItemHeight = 40;

    public static readonly StyledProperty<IEnumerable?> ItemsSourceProperty =
        AvaloniaProperty.Register<SelectDropdown, IEnumerable?>(nameof(ItemsSource));

    public static readonly StyledProperty<object?> SelectedItemProperty =
        AvaloniaProperty.Register<SelectDropdown, object?>(nameof(SelectedItem), defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<IDataTemplate?> ItemTemplateProperty =
        AvaloniaProperty.Register<SelectDropdown, IDataTemplate?>(nameof(ItemTemplate));

    public static readonly StyledProperty<string?> PlaceholderTextProperty =
        AvaloniaProperty.Register<SelectDropdown, string?>(nameof(PlaceholderText), "Select an option");

    private readonly Border _anchor;
    private readonly ContentControl _selectedContent;
    private readonly TextBlock _placeholder;
    private readonly Popup _popup;
    private readonly Border _popupCard;
    private readonly ListBox _list;
    private readonly TextBlock _empty;
    private bool _updatingSelection;

    public SelectDropdown()
    {
        Focusable = true;
        MinHeight = 42;

        _selectedContent = new ContentControl { VerticalContentAlignment = VerticalAlignment.Center };
        _placeholder = new TextBlock { VerticalAlignment = VerticalAlignment.Center };
        _placeholder.Bind(TextBlock.ForegroundProperty, new DynamicResourceExtension("MutedForeground"));
        var chevron = new HomisIcon
        {
            Kind = "ChevronDown",
            Width = 16,
            Height = 16,
            VerticalAlignment = VerticalAlignment.Center,
            IsHitTestVisible = false
        };
        chevron.Bind(HomisIcon.ForegroundProperty, new DynamicResourceExtension("MutedForeground"));
        var selectedHost = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            ColumnSpacing = 8,
            Children = { _selectedContent, _placeholder, At(chevron, 1) }
        };
        _anchor = new Border
        {
            MinHeight = 42,
            Padding = new Thickness(13, 7),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(7),
            Cursor = new Cursor(StandardCursorType.Hand),
            Child = selectedHost
        };
        _anchor.Bind(Border.BackgroundProperty, new DynamicResourceExtension("Card"));
        _anchor.Bind(Border.BorderBrushProperty, new DynamicResourceExtension("Border"));
        _anchor.PointerPressed += (_, e) =>
        {
            e.Handled = true;
            Focus();
            TogglePopup();
        };

        _list = new ListBox
        {
            BorderThickness = new Thickness(0),
            Background = Brushes.Transparent,
            SelectionMode = SelectionMode.Single
        };
        _list.SelectionChanged += OnSelectionChanged;
        _list.KeyDown += OnListKeyDown;
        Styles.Add(new Style(selector => selector.OfType<ListBoxItem>())
        {
            Setters =
            {
                new Setter(ListBoxItem.PaddingProperty, new Thickness(12, 9)),
                new Setter(ListBoxItem.MarginProperty, new Thickness(5, 2)),
                new Setter(ListBoxItem.CornerRadiusProperty, new CornerRadius(5)),
                new Setter(ListBoxItem.BackgroundProperty, Brushes.Transparent),
                new Setter(ListBoxItem.CursorProperty, new Cursor(StandardCursorType.Hand))
            }
        });
        Styles.Add(new Style(selector => selector.OfType<ListBoxItem>().Class(":pointerover").Template().OfType<ContentPresenter>())
        {
            Setters = { new Setter(ContentPresenter.BackgroundProperty, new DynamicResourceExtension("Hover")) }
        });
        Styles.Add(new Style(selector => selector.OfType<ListBoxItem>().Class(":selected").Template().OfType<ContentPresenter>())
        {
            Setters = { new Setter(ContentPresenter.BackgroundProperty, new DynamicResourceExtension("Selected")) }
        });

        _empty = new TextBlock
        {
            Text = "No options available",
            Margin = new Thickness(12),
            HorizontalAlignment = HorizontalAlignment.Center,
            IsVisible = false
        };
        _empty.Bind(TextBlock.ForegroundProperty, new DynamicResourceExtension("MutedForeground"));
        _popupCard = new Border
        {
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(7),
            Padding = new Thickness(0, 4),
            BoxShadow = new BoxShadows(new BoxShadow { OffsetY = 4, Blur = 14, Color = Color.FromArgb(42, 0, 0, 0) }),
            Child = new Grid { Children = { _list, _empty } }
        };
        _popupCard.Bind(Border.BackgroundProperty, new DynamicResourceExtension("Card"));
        _popupCard.Bind(Border.BorderBrushProperty, new DynamicResourceExtension("Border"));
        _popupCard.AddHandler(InputElement.PointerWheelChangedEvent, OnPopupWheel, RoutingStrategies.Bubble);
        _popup = new Popup
        {
            PlacementTarget = _anchor,
            Placement = PlacementMode.BottomEdgeAlignedLeft,
            IsLightDismissEnabled = true,
            Child = _popupCard
        };

        Children.Add(_anchor);
        Children.Add(_popup);
        KeyDown += OnKeyDown;
        UpdateSelectedContent();
    }

    public IEnumerable? ItemsSource { get => GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }
    public object? SelectedItem { get => GetValue(SelectedItemProperty); set => SetValue(SelectedItemProperty, value); }
    public IDataTemplate? ItemTemplate { get => GetValue(ItemTemplateProperty); set => SetValue(ItemTemplateProperty, value); }
    public string? PlaceholderText { get => GetValue(PlaceholderTextProperty); set => SetValue(PlaceholderTextProperty, value); }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ItemsSourceProperty) RefreshItems();
        else if (change.Property == SelectedItemProperty || change.Property == PlaceholderTextProperty)
        {
            UpdateSelectedContent();
            SyncSelection();
        }
        else if (change.Property == ItemTemplateProperty)
        {
            _selectedContent.ContentTemplate = ItemTemplate;
            _list.ItemTemplate = ItemTemplate;
        }
    }

    private void TogglePopup()
    {
        if (_popup.IsOpen)
        {
            _popup.IsOpen = false;
            return;
        }

        RefreshItems();
        _popupCard.Width = Math.Max(Bounds.Width, 160);
        _popup.IsOpen = true;
        Dispatcher.UIThread.Post(() =>
        {
            _list.Focus();
            if (_list.SelectedItem is not null) _list.ScrollIntoView(_list.SelectedItem);
        });
    }

    private void RefreshItems()
    {
        if (_list is null) return;
        var items = ItemsSource?.Cast<object>().ToArray() ?? [];
        _updatingSelection = true;
        _list.ItemsSource = items;
        _list.SelectedItem = items.FirstOrDefault(item => Equals(item, SelectedItem));
        _updatingSelection = false;
        _list.MaxHeight = Math.Min(items.Length, 6) * ItemHeight;
        _list.IsVisible = items.Length > 0;
        _empty.IsVisible = items.Length == 0;
    }

    private void SyncSelection()
    {
        if (_list is null || Equals(_list.SelectedItem, SelectedItem)) return;
        _updatingSelection = true;
        _list.SelectedItem = SelectedItem;
        _updatingSelection = false;
    }

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_updatingSelection || _list.SelectedItem is null) return;
        SetCurrentValue(SelectedItemProperty, _list.SelectedItem);
        _popup.IsOpen = false;
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key is Key.Enter or Key.Space or Key.Down)
        {
            if (!_popup.IsOpen) TogglePopup();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape && _popup.IsOpen)
        {
            _popup.IsOpen = false;
            e.Handled = true;
        }
    }

    private void OnListKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            _popup.IsOpen = false;
            Focus();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Enter && _list.SelectedItem is not null)
        {
            SetCurrentValue(SelectedItemProperty, _list.SelectedItem);
            _popup.IsOpen = false;
            Focus();
            e.Handled = true;
            return;
        }

        if (e.Key is not (Key.Down or Key.Up) || _list.ItemCount == 0) return;
        var offset = e.Key == Key.Down ? 1 : -1;
        var index = _list.SelectedIndex < 0 ? (offset > 0 ? 0 : _list.ItemCount - 1) : _list.SelectedIndex + offset;
        index = Math.Clamp(index, 0, _list.ItemCount - 1);
        _updatingSelection = true;
        _list.SelectedIndex = index;
        _updatingSelection = false;
        _list.ScrollIntoView(_list.SelectedItem!);
        e.Handled = true;
    }

    private void UpdateSelectedContent()
    {
        if (_selectedContent is null || _placeholder is null) return;
        _selectedContent.Content = SelectedItem;
        _selectedContent.ContentTemplate = ItemTemplate;
        _selectedContent.IsVisible = SelectedItem is not null;
        _placeholder.Text = PlaceholderText;
        _placeholder.IsVisible = SelectedItem is null;
    }

    private static void OnPopupWheel(object? sender, PointerWheelEventArgs e) => e.Handled = true;

    private static T At<T>(T control, int column) where T : Control
    {
        Grid.SetColumn(control, column);
        return control;
    }
}
