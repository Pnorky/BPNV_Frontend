using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using Avalonia.Styling;
using AvaloniaApp.ViewModels;
using Lucide.Avalonia;

namespace AvaloniaApp.Views;

public class MainWindow : Window
{
    public MainWindow()
    {
        Title = "Login - BPNV Convenience Store";
        Width = 520;
        Height = 500;
        MinWidth = 480;
        MinHeight = 460;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        this.BindResource(BackgroundProperty, "Background");
        this.BindResource(ForegroundProperty, "Foreground");
        ConfigureStyles();

        var username = new TextBox { PlaceholderText = "Enter username" };
        username.Bind(TextBox.TextProperty, new Binding(nameof(MainViewModel.Username)));
        username.BindResource(BackgroundProperty, "Card");
        username.BindResource(ForegroundProperty, "Foreground");

        var password = new TextBox { Name = "PasswordBox", PlaceholderText = "Enter password", PasswordChar = '*' };
        password.Bind(TextBox.TextProperty, new Binding(nameof(MainViewModel.Password)));
        password.BindResource(BackgroundProperty, "Card");
        password.BindResource(ForegroundProperty, "Foreground");

        var hiddenIcon = new LucideIcon
        {
            Kind = LucideIconKind.EyeOff,
            Width = 20,
            Height = 20,
            Foreground = Brushes.Black,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        hiddenIcon.Classes.Add("password-hidden");
        var visibleIcon = new LucideIcon
        {
            Kind = LucideIconKind.Eye,
            Width = 20,
            Height = 20,
            Foreground = Brushes.Black,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        visibleIcon.Classes.Add("password-visible");
        var passwordToggle = new ToggleButton
        {
            Width = 40,
            Height = 36,
            Margin = new Thickness(0, 0, 2, 0),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Cursor = new Cursor(StandardCursorType.Hand),
            Content = new Grid
            {
                Width = 20,
                Height = 20,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Children = { hiddenIcon, visibleIcon }
            }
        };
        passwordToggle.Classes.Add("password-toggle");
        passwordToggle.Bind(ToggleButton.IsCheckedProperty,
            new Binding(nameof(TextBox.RevealPassword)) { Source = password, Mode = BindingMode.TwoWay });
        passwordToggle.BindResource(ForegroundProperty, "Foreground");
        ToolTip.SetTip(passwordToggle, "Show or hide password");
        password.InnerRightContent = passwordToggle;

        var signIn = new Button
        {
            Content = "Sign In",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(16, 10),
            FontSize = 15,
            FontWeight = FontWeight.SemiBold,
            Cursor = new Cursor(StandardCursorType.Hand)
        };
        signIn.Bind(Button.CommandProperty, new Binding(nameof(MainViewModel.LoginCommand)));
        signIn.BindResource(BackgroundProperty, "Primary");
        signIn.BindResource(ForegroundProperty, "PrimaryForeground");

        var status = new TextBlock { HorizontalAlignment = HorizontalAlignment.Center };
        status.Bind(TextBlock.TextProperty, new Binding(nameof(MainViewModel.StatusMessage)));
        status.Bind(TextBlock.ForegroundProperty, new Binding(nameof(MainViewModel.StatusColor)));
        status.Bind(Visual.IsVisibleProperty, new Binding(nameof(MainViewModel.HasError)));

        var heading = new TextBlock
        {
            Text = "BPNV CONVENIENCE STORE",
            FontSize = 26,
            FontWeight = FontWeight.Bold,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        heading.BindResource(TextBlock.ForegroundProperty, "Primary");
        var subtitle = new TextBlock
        {
            Text = "Sales and Inventory Monitoring",
            FontSize = 12,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, -4, 0, 8)
        };
        subtitle.BindResource(TextBlock.ForegroundProperty, "MutedForeground");

        Content = new Grid
        {
            Children =
            {
                new Border
                {
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    Margin = new Thickness(56, 0),
                    Child = new StackPanel
                    {
                        Spacing = 16,
                        Children =
                        {
                            heading, subtitle,
                            new TextBlock { Text = "Username", FontSize = 13 }, username,
                            new TextBlock { Text = "Password", FontSize = 13 }, password,
                            signIn, status
                        }
                    }
                }
            }
        };
    }

    private void ConfigureStyles()
    {
        AddStyle(x => x.OfType<LucideIcon>().Class("password-visible"), new Setter(Visual.IsVisibleProperty, false));
        AddStyle(x => x.OfType<ToggleButton>().Class(":checked").Descendant().OfType<LucideIcon>().Class("password-hidden"),
            new Setter(Visual.IsVisibleProperty, false));
        AddStyle(x => x.OfType<ToggleButton>().Class(":checked").Descendant().OfType<LucideIcon>().Class("password-visible"),
            new Setter(Visual.IsVisibleProperty, true));
        AddStyle(x => x.OfType<ToggleButton>().Class("password-toggle").Class(":checked"),
            new Setter(TemplatedControl.BackgroundProperty, Brushes.Transparent),
            new Setter(TemplatedControl.BorderBrushProperty, Brushes.Transparent),
            new Setter(TemplatedControl.ForegroundProperty, new DynamicResourceExtension("Foreground")));
        AddStyle(x => x.OfType<ToggleButton>().Class("password-toggle").Class(":checked").Class(":pointerover"),
            new Setter(TemplatedControl.BackgroundProperty, Brushes.Transparent));
        AddStyle(x => x.OfType<ToggleButton>().Class("password-toggle").Class(":checked").Template().OfType<ContentPresenter>(),
            new Setter(ContentPresenter.BackgroundProperty, Brushes.Transparent));
    }

    private void AddStyle(Func<Selector?, Selector> selector, params Setter[] setters)
    {
        var style = new Style(selector);
        foreach (var setter in setters) style.Setters.Add(setter);
        Styles.Add(style);
    }
}

internal static class CodeOnlyResourceExtensions
{
    public static IDisposable BindResource(this AvaloniaObject target, AvaloniaProperty property, object key) =>
        target.Bind(property, new DynamicResourceExtension(key));
}
