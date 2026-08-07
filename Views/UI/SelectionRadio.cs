using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace AvaloniaApp.Views.UI;

public sealed class SelectionRadio : RadioButton
{
    protected override Type StyleKeyOverride => typeof(RadioButton);

    public SelectionRadio(string label, string? groupName = null, bool isChecked = false)
    {
        Content = label;
        GroupName = groupName;
        IsChecked = isChecked;
        FontSize = 14;
    }
}
