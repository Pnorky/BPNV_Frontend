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
        Width = 440;
        Height = 440;
        MinWidth = 400;
        MinHeight = 400;
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
            Padding = new Thickness(12, 7),
            FontSize = 15,
            FontWeight = FontWeight.SemiBold,
            Cursor = new Cursor(StandardCursorType.Hand)
        };
        signIn.Bind(Button.CommandProperty, new Binding(nameof(MainViewModel.LoginCommand)));
        signIn.Bind(Visual.IsVisibleProperty, new Binding(nameof(MainViewModel.CanLogin)));
        signIn.Bind(Button.IsDefaultProperty, new Binding(nameof(MainViewModel.CanLogin)));
        signIn.BindResource(BackgroundProperty, "Primary");
        signIn.BindResource(ForegroundProperty, "PrimaryForeground");

        var status = new TextBlock { HorizontalAlignment = HorizontalAlignment.Center };
        status.Bind(TextBlock.TextProperty, new Binding(nameof(MainViewModel.StatusMessage)));
        status.Bind(TextBlock.ForegroundProperty, new Binding(nameof(MainViewModel.StatusColor)));
        status.Bind(Visual.IsVisibleProperty, new Binding(nameof(MainViewModel.HasStatus)));

        var newPassword = new TextBox
        {
            PlaceholderText = "At least 12 characters",
            PasswordChar = '*'
        };
        newPassword.Bind(TextBox.TextProperty, new Binding(nameof(MainViewModel.NewPassword)));
        newPassword.BindResource(BackgroundProperty, "Card");
        newPassword.BindResource(ForegroundProperty, "Foreground");

        var confirmPassword = new TextBox
        {
            PlaceholderText = "Repeat new password",
            PasswordChar = '*'
        };
        confirmPassword.Bind(TextBox.TextProperty, new Binding(nameof(MainViewModel.ConfirmPassword)));
        confirmPassword.BindResource(BackgroundProperty, "Card");
        confirmPassword.BindResource(ForegroundProperty, "Foreground");

        var changePassword = new Button
        {
            Content = "Change Password and Continue",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            Padding = new Thickness(16, 10),
            FontWeight = FontWeight.SemiBold
        };
        changePassword.Bind(Button.CommandProperty, new Binding(nameof(MainViewModel.ChangePasswordCommand)));
        changePassword.Bind(Button.IsDefaultProperty, new Binding(nameof(MainViewModel.RequiresPasswordChange)));
        changePassword.BindResource(BackgroundProperty, "Primary");
        changePassword.BindResource(ForegroundProperty, "PrimaryForeground");

        var cancelPasswordChange = new Button
        {
            Content = "Cancel",
            HorizontalAlignment = HorizontalAlignment.Center,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0)
        };
        cancelPasswordChange.Bind(Button.CommandProperty, new Binding(nameof(MainViewModel.CancelPasswordChangeCommand)));

        var passwordChangePanel = new StackPanel
        {
            Spacing = 10,
            Children =
            {
                new TextBlock { Text = "New Password", FontSize = 13 },
                newPassword,
                new TextBlock { Text = "Confirm New Password", FontSize = 13 },
                confirmPassword,
                changePassword,
                cancelPasswordChange
            }
        };
        passwordChangePanel.Bind(Visual.IsVisibleProperty, new Binding(nameof(MainViewModel.RequiresPasswordChange)));

        var heading = new TextBlock
        {
            Text = "BPNV CONVENIENCE STORE",
            FontSize = 21,
            FontWeight = FontWeight.Bold,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        heading.BindResource(TextBlock.ForegroundProperty, "Primary");
        var subtitle = new TextBlock
        {
            Text = "Sales and Inventory Monitoring",
            FontSize = 12,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, -5, 0, 2)
        };
        subtitle.BindResource(TextBlock.ForegroundProperty, "MutedForeground");

        var loginCard = new Border
        {
            Width = 380,
            MaxWidth = 380,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(16),
            Padding = new Thickness(22, 20),
            CornerRadius = new CornerRadius(16),
            BorderThickness = new Thickness(1),
            Child = new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    heading, subtitle,
                    new TextBlock { Text = "Username", FontSize = 12 }, username,
                    new TextBlock { Text = "Password", FontSize = 12 }, password,
                    signIn, passwordChangePanel, status
                }
            }
        };
        loginCard.Classes.Add("theme-card");
        loginCard.BindResource(BackgroundProperty, "Card");
        loginCard.BindResource(BorderBrushProperty, "Border");

        Content = new Grid
        {
            Children =
            {
                loginCard
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
