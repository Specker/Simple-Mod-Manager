#if !WINDOWS
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using FluentAvalonia.UI.Controls;

namespace VintageStoryModManager.Services;

internal static class AvaloniaConfirmationDialogService
{
    public static bool ShowYesNoWarning(string message, string title)
    {
        try
        {
            return Dispatcher.UIThread.InvokeAsync(async () =>
            {
                var dialog = new ContentDialog
                {
                    Title = title,
                    Content = message,
                    PrimaryButtonText = "Yes",
                    CloseButtonText = "No",
                    DefaultButton = ContentDialogButton.Close
                };

                var result = Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime
                    {
                        MainWindow: { } mainWindow
                    }
                    ? await dialog.ShowAsync(mainWindow)
                    : await dialog.ShowAsync();

                return result == ContentDialogResult.Primary;
            }).GetAwaiter().GetResult();
        }
        catch
        {
            return false;
        }
    }
}
#endif
