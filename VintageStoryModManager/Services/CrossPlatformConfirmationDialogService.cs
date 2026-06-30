namespace VintageStoryModManager.Services;

public static class CrossPlatformConfirmationDialogService
{
    public static bool ShowYesNoWarning(string message, string title)
    {
#if WINDOWS
        var result = ModManagerMessageBox.Show(
            message,
            title,
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);

        return result == System.Windows.MessageBoxResult.Yes;
#else
        return AvaloniaConfirmationDialogService.ShowYesNoWarning(message, title);
#endif
    }
}
