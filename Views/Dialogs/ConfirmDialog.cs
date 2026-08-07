using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using Avalonia.Styling;

namespace AvaloniaApp.Views.Dialogs;

public class ConfirmDialog : Window
{
    private readonly TextBlock _dialogTitleText;
    private readonly TextBlock _messageText;
    private readonly Button _cancelButton;
    private readonly Button _confirmButton;

    public bool Confirmed { get; private set; }

    public ConfirmDialog()
    {
        Title = "Confirm";
        Width = 440;
        MinHeight = 210;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        CanResize = false;
        this.BindResource(BackgroundProperty, "Card");
        this.BindResource(ForegroundProperty, "Foreground");
        ConfigureStyles();

        _dialogTitleText = new TextBlock { Text = "Confirm", FontSize = 20, FontWeight = FontWeight.SemiBold };
        _dialogTitleText.BindResource(TextBlock.ForegroundProperty, "Foreground");
        _messageText = new TextBlock
        {
            FontSize = 14,
            LineHeight = 21,
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 384
        };
        _messageText.BindResource(TextBlock.ForegroundProperty, "MutedForeground");
        _cancelButton = CreateButton("Cancel", "dialog-cancel");
        _confirmButton = CreateButton("Confirm", "dialog-confirm");
        _confirmButton.Click += (_, _) => { Confirmed = true; Close(); };
        _cancelButton.Click += (_, _) => Close();

        var actions = new Border
        {
            Padding = new Thickness(28, 16, 28, 22),
            BorderThickness = new Thickness(0, 1, 0, 0),
            Child = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Spacing = 10,
                Children = { _cancelButton, _confirmButton }
            }
        };
        actions.BindResource(Border.BorderBrushProperty, "Border");
        Grid.SetRow(actions, 1);
        var border = new Border
        {
            Child = new Grid
            {
                RowDefinitions = new RowDefinitions("Auto,Auto"),
                Children =
                {
                    new StackPanel
                    {
                        Margin = new Thickness(28, 26, 28, 22),
                        Spacing = 8,
                        Children = { _dialogTitleText, _messageText }
                    },
                    actions
                }
            }
        };
        border.Classes.Add("theme-dialog");
        border.BindResource(Border.BackgroundProperty, "Card");
        Content = border;
    }

    public void SetInformation(string title, string message, string buttonText = "Close")
    {
        Configure(title, message, buttonText, showCancel: false, destructive: false);
    }

    public void SetConfirmation(string title, string message, string buttonText = "Confirm")
    {
        Configure(title, message, buttonText, showCancel: true, destructive: true);
    }

    private void Configure(string title, string message, string buttonText, bool showCancel, bool destructive)
    {
        Title = title;
        _dialogTitleText.Text = title;
        _messageText.Text = message;
        _confirmButton.Content = buttonText;
        _cancelButton.IsVisible = showCancel;

        _confirmButton.Classes.Remove("destructive");
        if (destructive)
            _confirmButton.Classes.Add("destructive");
    }

    private static Button CreateButton(string content, string styleClass)
    {
        var button = new Button { Content = content };
        button.Classes.Add("dialog-action");
        button.Classes.Add(styleClass);
        return button;
    }

    private void ConfigureStyles()
    {
        AddStyle(x => x.OfType<Button>().Class("dialog-action"),
            new Setter(Button.MinWidthProperty, 88d), new Setter(Button.HeightProperty, 36d),
            new Setter(Button.PaddingProperty, new Thickness(14, 6)),
            new Setter(Button.CornerRadiusProperty, new CornerRadius(6)),
            new Setter(Button.FontWeightProperty, FontWeight.SemiBold),
            new Setter(Button.HorizontalContentAlignmentProperty, HorizontalAlignment.Center),
            new Setter(Button.VerticalContentAlignmentProperty, VerticalAlignment.Center),
            new Setter(InputElement.CursorProperty, new Cursor(StandardCursorType.Hand)));
        AddStyle(x => x.OfType<Button>().Class("dialog-cancel"),
            new Setter(Button.BackgroundProperty, new DynamicResourceExtension("Secondary")),
            new Setter(Button.ForegroundProperty, new DynamicResourceExtension("SecondaryForeground")));
        AddStyle(x => x.OfType<Button>().Class("dialog-confirm"),
            new Setter(Button.BackgroundProperty, new DynamicResourceExtension("Primary")),
            new Setter(Button.ForegroundProperty, new DynamicResourceExtension("PrimaryForeground")));
        AddStyle(x => x.OfType<Button>().Class("dialog-confirm").Class("destructive"),
            new Setter(Button.BackgroundProperty, new DynamicResourceExtension("Destructive")),
            new Setter(Button.ForegroundProperty, new DynamicResourceExtension("DestructiveForeground")));
    }

    private void AddStyle(Func<Selector?, Selector> selector, params Setter[] setters)
    {
        var style = new Style(selector);
        foreach (var setter in setters) style.Setters.Add(setter);
        Styles.Add(style);
    }
}
