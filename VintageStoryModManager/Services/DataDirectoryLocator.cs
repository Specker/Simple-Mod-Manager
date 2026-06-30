using System.IO;

namespace VintageStoryModManager.Services;

/// <summary>
///     Resolves the Vintage Story data directory in the same way as the game.
/// </summary>
public static class DataDirectoryLocator
{
    private static readonly string DataFolderName = DevConfig.DataFolderName;

    /// <summary>
    ///     Returns the absolute path to the Vintage Story data directory.
    /// </summary>
    public static string Resolve()
    {
        foreach (var candidate in EnumerateCandidates())
            if (!string.IsNullOrWhiteSpace(candidate))
                return candidate;

        return string.Empty;
    }

    private static IEnumerable<string> EnumerateCandidates()
    {
        foreach (var linuxPath in EnumerateLinuxDataPaths())
            if (!string.IsNullOrWhiteSpace(linuxPath))
                yield return linuxPath!;

        var fromAppData = TryCombine(GetFolder(Environment.SpecialFolder.ApplicationData), DataFolderName);
        if (!string.IsNullOrWhiteSpace(fromAppData)) yield return fromAppData!;

        var fromLocalAppData = TryCombine(GetFolder(Environment.SpecialFolder.LocalApplicationData), DataFolderName);
        if (!string.IsNullOrWhiteSpace(fromLocalAppData)) yield return fromLocalAppData!;

        var portable = GetPortableCandidate();
        if (!string.IsNullOrWhiteSpace(portable)) yield return portable!;

        foreach (var root in EnumerateAdditionalWindowsRoots())
        {
            var candidate = TryCombine(root, DataFolderName);
            if (!string.IsNullOrWhiteSpace(candidate)) yield return candidate!;
        }

        var currentDirectory = TryNormalize(Directory.GetCurrentDirectory());
        if (!string.IsNullOrWhiteSpace(currentDirectory)) yield return currentDirectory!;
    }

    private static string? GetFolder(Environment.SpecialFolder folder)
    {
        try
        {
            var path = Environment.GetFolderPath(folder, Environment.SpecialFolderOption.DoNotVerify);
            if (!string.IsNullOrWhiteSpace(path)) return path;
        }
        catch (PlatformNotSupportedException)
        {
            return null;
        }

        return null;
    }

    private static string? GetPortableCandidate()
    {
        var baseDirectory = AppContext.BaseDirectory;

        foreach (var folder in new[] { DataFolderName, "data" })
        {
            var candidate = TryCombine(baseDirectory, folder);
            if (!string.IsNullOrWhiteSpace(candidate) && Directory.Exists(candidate)) return candidate;
        }

        return null;
    }

    private static IEnumerable<string> EnumerateAdditionalWindowsRoots()
    {
        if (!OperatingSystem.IsWindows()) yield break;

        foreach (var root in new[]
                 {
                     @"C:\\Games",
                     @"D:\\Games",
                     @"C:\\Program Files",
                     @"C:\\Program Files (x86)"
                 })
        {
            var normalized = TryNormalize(root);
            if (!string.IsNullOrWhiteSpace(normalized)) yield return normalized!;
        }
    }

    private static IEnumerable<string> EnumerateLinuxDataPaths()
    {
        if (!OperatingSystem.IsLinux()) yield break;

        var xdgConfigHome = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        var configBase = !string.IsNullOrWhiteSpace(xdgConfigHome)
            ? xdgConfigHome
            : TryCombine(GetHomeDirectory(), ".config");

        var xdgDataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        var dataBase = !string.IsNullOrWhiteSpace(xdgDataHome)
            ? xdgDataHome
            : TryCombine(GetHomeDirectory(), ".local/share");

        var configCandidate = TryCombine(configBase, DataFolderName);
        if (!string.IsNullOrWhiteSpace(configCandidate)) yield return configCandidate!;

        var dataCandidate = TryCombine(dataBase, DataFolderName);
        if (!string.IsNullOrWhiteSpace(dataCandidate)) yield return dataCandidate!;
    }

    private static string? GetHomeDirectory()
    {
        var fromEnvironment = Environment.GetEnvironmentVariable("HOME");
        if (!string.IsNullOrWhiteSpace(fromEnvironment)) return fromEnvironment;

        var fromSpecialFolder = GetFolder(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(fromSpecialFolder)) return fromSpecialFolder;

        return null;
    }

    private static string? TryCombine(string? basePath, string relative)
    {
        if (string.IsNullOrWhiteSpace(basePath)) return null;

        try
        {
            return Path.GetFullPath(Path.Combine(basePath, relative));
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static string? TryNormalize(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;

        try
        {
            return Path.GetFullPath(path);
        }
        catch (Exception)
        {
            return null;
        }
    }
}