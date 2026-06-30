#if !WINDOWS
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using System.Diagnostics;
using System.IO;
using System.Runtime.ExceptionServices;
using VintageStoryModManager.Views;

namespace VintageStoryModManager;

public partial class App : Application
{
    private static readonly string SingleInstanceMutexName =
        $"VintageStoryModManager.SingleInstance.{Environment.UserName}";
    private Mutex? _instanceMutex;
    private bool _ownsMutex;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        VerifyLinuxSpecialFolderMappings();
        AppDomain.CurrentDomain.FirstChanceException += OnFirstChanceException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        Dispatcher.UIThread.UnhandledException += OnUiThreadUnhandledException;

        if (!AcquireSingleInstance())
        {
            ShowSingleInstanceWarning();
            if (ApplicationLifetime is IControlledApplicationLifetime lifetime) lifetime.Shutdown(-1);
            return;
        }

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;
            desktop.MainWindow ??= new MainWindow();
            desktop.Exit += OnDesktopExit;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void OnDesktopExit(object? sender, ControlledApplicationLifetimeExitEventArgs e)
    {
        DisposeSingleInstance();
        AppDomain.CurrentDomain.FirstChanceException -= OnFirstChanceException;
        AppDomain.CurrentDomain.UnhandledException -= OnUnhandledException;
        Dispatcher.UIThread.UnhandledException -= OnUiThreadUnhandledException;
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

    private bool AcquireSingleInstance()
    {
        try
        {
            var mutex = new Mutex(true, SingleInstanceMutexName, out var createdNew);
            _instanceMutex = mutex;
            _ownsMutex = createdNew;
            return createdNew;
        }
        catch (UnauthorizedAccessException ex)
        {
            Debug.WriteLine($"[SingleInstance] Unable to acquire mutex: {ex.Message}");
            return false;
        }
        catch (IOException ex)
        {
            Debug.WriteLine($"[SingleInstance] Unable to acquire mutex: {ex.Message}");
            return false;
        }
    }

    private void DisposeSingleInstance()
    {
        if (_instanceMutex == null) return;

        if (_ownsMutex) _instanceMutex.ReleaseMutex();
        _instanceMutex.Dispose();
        _instanceMutex = null;
        _ownsMutex = false;
    }

    private static void OnFirstChanceException(object? sender, FirstChanceExceptionEventArgs e)
    {
        if (e.Exception is not InvalidOperationException) return;
        Debug.WriteLine($"[FirstChance] InvalidOperationException: {e.Exception.Message}");
        Debug.WriteLine(e.Exception.StackTrace);
    }

    private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        var exception = e.ExceptionObject as Exception;
        Debug.WriteLine($"[UnhandledException] {exception?.Message ?? "Unknown error"}");
    }

    private static void OnUiThreadUnhandledException(object? sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Debug.WriteLine($"[UIUnhandledException] {e.Exception.Message}");
        e.Handled = false;
    }

    private static void ShowSingleInstanceWarning()
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
            return;

        var dialog = new Window
        {
            Title = "Simple VS Manager",
            Width = 460,
            Height = 170,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            Content = BuildSingleInstanceDialogContent()
        };

        if (desktop.MainWindow is { } owner)
            dialog.ShowDialog(owner);
        else
            dialog.Show();
    }

    private static Control BuildSingleInstanceDialogContent()
    {
        var message = new TextBlock
        {
            Text = "Simple VS Manager is already running. This launch will be aborted.",
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 16)
        };

        var button = new Button
        {
            Content = "Abort",
            Width = 100,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right
        };

        var panel = new StackPanel
        {
            Margin = new Thickness(16)
        };
        panel.Children.Add(message);
        panel.Children.Add(button);

        button.Click += (_, _) =>
        {
            var window = TopLevel.GetTopLevel(button) as Window;
            window?.Close();
        };

        return panel;
    }
}
#endif
