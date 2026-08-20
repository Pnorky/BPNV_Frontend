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

public sealed class SearchableSelect : Grid
{
    private const double ItemHeight = 42;

    public static readonly StyledProperty<IEnumerable?> ItemsSourceProperty =
        AvaloniaProperty.Register<SearchableSelect, IEnumerable?>(nameof(ItemsSource));

    public static readonly StyledProperty<object?> SelectedItemProperty =
        AvaloniaProperty.Register<SearchableSelect, object?>(nameof(SelectedItem), defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<IDataTemplate?> ItemTemplateProperty =
        AvaloniaProperty.Register<SearchableSelect, IDataTemplate?>(nameof(ItemTemplate));

    public static readonly StyledProperty<string?> PlaceholderTextProperty =
        AvaloniaProperty.Register<SearchableSelect, string?>(nameof(PlaceholderText), "Select an option");

    public static readonly StyledProperty<bool> AllowCustomValueProperty =
        AvaloniaProperty.Register<SearchableSelect, bool>(nameof(AllowCustomValue));

    private readonly Border _anchor;
    private readonly ContentControl _selectedContent;
    private readonly TextBlock _placeholder;
    private readonly Popup _popup;
    private readonly Border _popupCard;
    private readonly TextBox _search;
    private readonly ListBox _list;
    private readonly TextBlock _empty;
    private bool _updatingSelection;

    public SearchableSelect()
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
            Children = { _selectedContent, _placeholder, At(chevron, 1) }
        };
        _anchor = new Border
        {
            MinHeight = 42,
            Padding = new Thickness(13, 7),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(7),
            Child = selectedHost,
            Cursor = new Cursor(StandardCursorType.Hand)
        };
        _anchor.Bind(Border.BackgroundProperty, new DynamicResourceExtension("Card"));
        _anchor.Bind(Border.BorderBrushProperty, new DynamicResourceExtension("Border"));
        _anchor.PointerPressed += (_, e) =>
        {
            e.Handled = true;
            TogglePopup();
        };

        _search = new TextBox { PlaceholderText = "Search options...", Margin = new Thickness(8) };
        _search.Classes.Add("form-input");
        _search.TextChanged += (_, _) => RefreshItems();
        _search.KeyDown += OnSearchKeyDown;

        _list = new ListBox
        {
            BorderThickness = new Thickness(0),
            Background = Brushes.Transparent,
            SelectionMode = SelectionMode.Single
        };
        _list.SelectionChanged += OnSelectionChanged;
        Styles.Add(new Style(selector => selector.OfType<ListBoxItem>())
        {
            Setters =
            {
                new Setter(ListBoxItem.PaddingProperty, new Thickness(12, 9)),
                new Setter(ListBoxItem.MarginProperty, new Thickness(6, 2)),
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
            Text = "No matching options",
            Margin = new Thickness(12),
            HorizontalAlignment = HorizontalAlignment.Center,
            IsVisible = false
        };
        _empty.Bind(TextBlock.ForegroundProperty, new DynamicResourceExtension("MutedForeground"));

        _popupCard = new Border
        {
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(7),
            Padding = new Thickness(0, 0, 0, 6),
            BoxShadow = new BoxShadows(new BoxShadow { OffsetY = 4, Blur = 14, Color = Color.FromArgb(42, 0, 0, 0) }),
            Child = new StackPanel { Children = { _search, _list, _empty } }
        };
        _popupCard.Bind(Border.BackgroundProperty, new DynamicResourceExtension("Card"));
        _popupCard.Bind(Border.BorderBrushProperty, new DynamicResourceExtension("Border"));
        _popupCard.AddHandler(InputElement.PointerWheelChangedEvent, OnPopupWheel, RoutingStrategies.Bubble);
        _popup = new Popup
        {
            PlacementTarget = _anchor,
            Placement = PlacementMode.Bottom,
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
    public bool AllowCustomValue { get => GetValue(AllowCustomValueProperty); set => SetValue(AllowCustomValueProperty, value); }
    public Func<object, string>? SearchTextSelector { get; set; }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ItemsSourceProperty) RefreshItems();
        else if (change.Property == SelectedItemProperty || change.Property == PlaceholderTextProperty) UpdateSelectedContent();
        else if (change.Property == ItemTemplateProperty)
        {
            _selectedContent.ContentTemplate = ItemTemplate;
            _list.ItemTemplate = ItemTemplate;
        }
    }

    private void TogglePopup()
    {
        if (_popup.IsOpen) { _popup.IsOpen = false; return; }
        _search.Text = string.Empty;
        RefreshItems();
        _popupCard.Width = Math.Max(Bounds.Width, 180);
        _popup.IsOpen = true;
        Dispatcher.UIThread.Post(() => _search.Focus());
    }

    private void RefreshItems()
    {
        if (_list is null) return;
        var source = ItemsSource?.Cast<object>() ?? [];
        var search = _search?.Text?.Trim();
        var filtered = string.IsNullOrWhiteSpace(search)
            ? source.ToArray()
            : source.Where(item => SearchText(item).Contains(search, StringComparison.OrdinalIgnoreCase)).ToArray();
        _updatingSelection = true;
        _list.ItemsSource = filtered;
        _list.SelectedItem = filtered.FirstOrDefault(item => Equals(item, SelectedItem));
        _updatingSelection = false;
        _list.MaxHeight = Math.Min(filtered.Length, 5) * ItemHeight;
        _list.IsVisible = filtered.Length > 0;
        _empty.IsVisible = filtered.Length == 0;
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
            e.Handled = true;
            if (!_popup.IsOpen) TogglePopup();
        }
    }

    private void OnSearchKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) { _popup.IsOpen = false; e.Handled = true; }
        else if (e.Key == Key.Down && _list.ItemCount > 0) { _list.Focus(); _list.SelectedIndex = 0; e.Handled = true; }
        else if (e.Key == Key.Enter && _list.ItemCount > 0)
        {
            _list.SelectedIndex = Math.Max(0, _list.SelectedIndex);
            e.Handled = true;
        }
        else if (e.Key == Key.Enter && AllowCustomValue && !string.IsNullOrWhiteSpace(_search.Text))
        {
            SetCurrentValue(SelectedItemProperty, _search.Text.Trim());
            _popup.IsOpen = false;
            e.Handled = true;
        }
    }

    private static void OnPopupWheel(object? sender, PointerWheelEventArgs e) => e.Handled = true;

    private void UpdateSelectedContent()
    {
        if (_selectedContent is null || _placeholder is null) return;
        _selectedContent.Content = SelectedItem;
        _selectedContent.ContentTemplate = ItemTemplate;
        _selectedContent.IsVisible = SelectedItem is not null;
        _placeholder.Text = PlaceholderText;
        _placeholder.IsVisible = SelectedItem is null;
    }

    private string SearchText(object item) => SearchTextSelector?.Invoke(item) ?? item.ToString() ?? string.Empty;
    private static T At<T>(T control, int column) where T : Control { Grid.SetColumn(control, column); return control; }
}
