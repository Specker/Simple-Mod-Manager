#if !WINDOWS
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using System.Diagnostics;
using VintageStoryModManager.Views;

namespace VintageStoryModManager;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        VerifyLinuxSpecialFolderMappings();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;
            desktop.MainWindow ??= new MainWindow();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void VerifyLinuxSpecialFolderMappings()
    {
        if (!OperatingSystem.IsLinux()) return;

        var localApplicationData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var applicationData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        if (!IsLinuxPathMappingExpected(localApplicationData, ".local/share"))
            Debug.WriteLine(
                $"[LinuxPathCheck] LocalApplicationData resolved to unexpected path '{localApplicationData}'.");

        if (!IsLinuxPathMappingExpected(applicationData, ".config"))
            Debug.WriteLine($"[LinuxPathCheck] ApplicationData resolved to unexpected path '{applicationData}'.");
    }

    private static bool IsLinuxPathMappingExpected(string resolvedPath, string expectedSuffix)
    {
        if (string.IsNullOrWhiteSpace(resolvedPath)) return false;

        var normalized = resolvedPath.Replace('\\', '/').TrimEnd('/');
        return normalized.EndsWith(expectedSuffix, StringComparison.Ordinal);
    }
}
#endif
