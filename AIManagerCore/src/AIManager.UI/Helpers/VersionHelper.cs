using System.IO;
using System.Reflection;

namespace AIManager.UI.Helpers;

/// <summary>
/// Helper class for reading application version from VERSION file
/// </summary>
public static class VersionHelper
{
    private static string? _cachedVersion;

    /// <summary>
    /// Gets the application version from the VERSION file
    /// Falls back to assembly version if file not found
    /// </summary>
    public static string GetVersion()
    {
        if (_cachedVersion != null)
            return _cachedVersion;

        _cachedVersion = ReadVersionFromFile() ?? GetAssemblyVersion();
        return _cachedVersion;
    }

    /// <summary>
    /// Gets the formatted version string with 'v' prefix (e.g., "v1.4.0")
    /// </summary>
    public static string GetVersionWithPrefix() => $"v{GetVersion()}";

    /// <summary>
    /// Gets the formatted version string with 'Version' prefix (e.g., "Version 1.4.0")
    /// </summary>
    public static string GetVersionDisplay() => $"Version {GetVersion()}";

    private static string? ReadVersionFromFile()
    {
        try
        {
            // Try multiple paths to find VERSION file
            var searchPaths = new[]
            {
                // Development: project root
                Path.Combine(GetProjectRoot(), "VERSION"),
                // Published: next to exe
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "VERSION"),
                // Published: parent directories
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "VERSION"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "VERSION"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "VERSION"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "VERSION"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..", "VERSION"),
            };

            foreach (var path in searchPaths)
            {
                var fullPath = Path.GetFullPath(path);
                if (File.Exists(fullPath))
                {
                    var version = File.ReadAllText(fullPath).Trim();
                    if (!string.IsNullOrWhiteSpace(version))
                    {
                        return version;
                    }
                }
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    private static string GetProjectRoot()
    {
        // Try to find project root by looking for VERSION file or .git folder
        var directory = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);

        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "VERSION")) ||
                Directory.Exists(Path.Combine(directory.FullName, ".git")))
            {
                return directory.FullName;
            }
            directory = directory.Parent;
        }

        return AppDomain.CurrentDomain.BaseDirectory;
    }

    private static string GetAssemblyVersion()
    {
        try
        {
            var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
            var version = assembly.GetName().Version;
            if (version != null)
            {
                return $"{version.Major}.{version.Minor}.{version.Build}";
            }
        }
        catch
        {
            // Ignore
        }

        return "1.0.0";
    }

    /// <summary>
    /// Clears the cached version (useful for testing or if VERSION file changes)
    /// </summary>
    public static void ClearCache()
    {
        _cachedVersion = null;
    }
}
