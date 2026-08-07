using Avalonia.Controls;

namespace AvaloniaApp.Views.UI;

public sealed class SelectionCheckbox : CheckBox
{
    protected override Type StyleKeyOverride => typeof(CheckBox);

    public SelectionCheckbox(string label, bool isChecked = false)
    {
        Content = label;
        IsChecked = isChecked;
        FontSize = 14;
    }
}
