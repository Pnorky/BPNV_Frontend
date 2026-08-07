using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;

namespace AvaloniaApp.Views.UI;

public sealed class PagedTableColumn : AvaloniaObject
{
    public static readonly StyledProperty<string> HeaderProperty =
        AvaloniaProperty.Register<PagedTableColumn, string>(nameof(Header), "Column");
    public static readonly StyledProperty<string> PropertyNameProperty =
        AvaloniaProperty.Register<PagedTableColumn, string>(nameof(PropertyName), "");
    public static readonly StyledProperty<GridLength> WidthProperty =
        AvaloniaProperty.Register<PagedTableColumn, GridLength>(nameof(Width), new GridLength(1, GridUnitType.Star));
    public static readonly StyledProperty<bool> IsSortableProperty =
        AvaloniaProperty.Register<PagedTableColumn, bool>(nameof(IsSortable), true);
    public static readonly StyledProperty<IDataTemplate?> CellTemplateProperty =
        AvaloniaProperty.Register<PagedTableColumn, IDataTemplate?>(nameof(CellTemplate));

    public string Header { get => GetValue(HeaderProperty); set => SetValue(HeaderProperty, value); }
    public string PropertyName { get => GetValue(PropertyNameProperty); set => SetValue(PropertyNameProperty, value); }
    public GridLength Width { get => GetValue(WidthProperty); set => SetValue(WidthProperty, value); }
    public bool IsSortable { get => GetValue(IsSortableProperty); set => SetValue(IsSortableProperty, value); }
    public IDataTemplate? CellTemplate { get => GetValue(CellTemplateProperty); set => SetValue(CellTemplateProperty, value); }
    public HorizontalAlignment HorizontalAlignment { get; set; } = HorizontalAlignment.Stretch;

    // Prefer these delegates in trimmed/AOT applications. PropertyName remains available for concise XAML usage.
    public Func<object, object?>? ValueSelector { get; set; }
    public Func<object, object?>? SortValueSelector { get; set; }

    public static PagedTableColumn Create<T, TValue>(string header, Func<T, TValue> valueSelector,
        GridLength? width = null, bool isSortable = true) where T : class => new()
    {
        Header = header,
        Width = width ?? new GridLength(1, GridUnitType.Star),
        IsSortable = isSortable,
        ValueSelector = item => valueSelector((T)item),
        SortValueSelector = item => valueSelector((T)item)
    };

    internal object? GetCellValue(object item) => ValueSelector?.Invoke(item) ?? GetPropertyValue(item);
    internal object? GetSortValue(object item) => SortValueSelector?.Invoke(item) ?? GetCellValue(item);

    private object? GetPropertyValue(object item) => string.IsNullOrWhiteSpace(PropertyName)
        ? item
        : item.GetType().GetProperty(PropertyName)?.GetValue(item);
}
