using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using AvaloniaApp.Services;
using AvaloniaApp.Views.UI;

namespace AvaloniaApp.Views.Dialogs;

public sealed class SupplierEditDialog : Window
{
    private readonly FormInput _name;
    private readonly FormInput _contact;
    private readonly FormInput _phone;
    private readonly TextBlock _validation;

    public SupplierEditDialog(SupplierResponse supplier)
    {
        Title = "Edit supplier";
        Width = 460;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        CanResize = false;

        _name = new FormInput("Supplier name", "Required") { Text = supplier.Name };
        _contact = new FormInput("Contact person", "Optional") { Text = supplier.ContactPerson };
        _phone = new FormInput("Phone", "Optional") { Text = supplier.Phone };
        _validation = new TextBlock { IsVisible = false, FontSize = 12 };
        _validation.Bind(TextBlock.ForegroundProperty, new DynamicResourceExtension("Destructive"));

        var cancel = new ActionButton("Cancel", ActionButtonVariant.Secondary);
        cancel.Click += (_, _) => Close();
        var save = new ActionButton("Save changes");
        save.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(NameValue))
            {
                _validation.Text = "Supplier name is required.";
                _validation.IsVisible = true;
                return;
            }

            Confirmed = true;
            Close();
        };

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
            Children = { cancel, save }
        };
        Content = new Border
        {
            Padding = new Thickness(24),
            Child = new StackPanel
            {
                Spacing = 16,
                Children =
                {
                    new TextBlock { Text = "Edit supplier", FontSize = 20, FontWeight = FontWeight.SemiBold },
                    _name, _contact, _phone, _validation, actions
                }
            }
        };
    }

    public bool Confirmed { get; private set; }
    public string NameValue => _name.Text?.Trim() ?? string.Empty;
    public string? ContactValue => NullIfWhiteSpace(_contact.Text);
    public string? PhoneValue => NullIfWhiteSpace(_phone.Text);

    private static string? NullIfWhiteSpace(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
