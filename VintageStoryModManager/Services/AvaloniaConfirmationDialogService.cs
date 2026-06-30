#if !WINDOWS
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using FluentAvalonia.UI.Controls;
using System.Diagnostics;

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
        catch (InvalidOperationException ex)
        {
            Debug.WriteLine($"[AvaloniaConfirmationDialogService] Invalid operation while showing dialog: {ex}");
            return false;
        }
        catch (TaskCanceledException ex)
        {
            Debug.WriteLine($"[AvaloniaConfirmationDialogService] Dialog task cancelled: {ex}");
            return false;
        }
        catch (ObjectDisposedException ex)
        {
            Debug.WriteLine($"[AvaloniaConfirmationDialogService] Dialog owner disposed: {ex}");
            return false;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AvaloniaConfirmationDialogService] Unexpected error while showing dialog: {ex}");
            return false;
        }
    }
}
#endif
