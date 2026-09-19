using Avalonia.Controls;

namespace AvaloniaApp.Services;

public interface IInputValidationNotifier
{
    void ShowInputValidationError(string message);
}

public static class InputValidationNotifier
{
    public static void Notify(Control control, string message)
    {
        var topLevel = TopLevel.GetTopLevel(control);
        if (topLevel?.DataContext is IInputValidationNotifier notifier)
        {
            notifier.ShowInputValidationError(message);
            return;
        }

        if (topLevel is Window { Owner.DataContext: IInputValidationNotifier ownerNotifier })
            ownerNotifier.ShowInputValidationError(message);
    }
}
