using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using AvaloniaApp.Services;
using AvaloniaApp.Views.UI;

namespace AvaloniaApp.Views.Dialogs;

public sealed class EmployeeEditDialog : Window
{
    private readonly FormInput _name;
    private readonly TextBlock _validation;

    public EmployeeEditDialog(EmployeeResponse employee)
    {
        Title = "Edit employee";
        Width = 440;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        CanResize = false;
        _name = new FormInput("Employee name", "Required") { Text = employee.Name };
        _validation = new TextBlock { IsVisible = false, FontSize = 12 };
        _validation.Bind(TextBlock.ForegroundProperty, new DynamicResourceExtension("Destructive"));
        var cancel = new ActionButton("Cancel", ActionButtonVariant.Secondary);
        cancel.Click += (_, _) => Close();
        var save = new ActionButton("Save changes");
        save.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(NameValue)) { _validation.Text = "Employee name is required."; _validation.IsVisible = true; return; }
            Confirmed = true;
            Close();
        };
        Content = new Border { Padding = new Thickness(24), Child = new StackPanel { Spacing = 16, Children =
        {
            new TextBlock { Text = $"Edit {employee.EmployeeNumber}", FontSize = 20, FontWeight = FontWeight.SemiBold },
            _name, _validation,
            new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 8, Children = { cancel, save } }
        } } };
    }

    public bool Confirmed { get; private set; }
    public string NameValue => _name.Text?.Trim() ?? string.Empty;
}
