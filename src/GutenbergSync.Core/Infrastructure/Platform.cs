namespace GutenbergSync.Core.Infrastructure;

/// <summary>
/// Represents the operating system platform
/// </summary>
public enum Platform
{
    /// <summary>
    /// Unknown platform
    /// </summary>
    Unknown,

    /// <summary>
    /// Windows
    /// </summary>
    Windows,

    /// <summary>
    /// Linux
    /// </summary>
    Linux,

    /// <summary>
    /// macOS
    /// </summary>
    MacOS
}

/// <summary>
/// Platform detection utilities
/// </summary>
public static class PlatformDetector
{
    /// <summary>
    /// Gets the current platform
    /// </summary>
    public static Platform GetCurrentPlatform()
    {
        if (OperatingSystem.IsWindows())
            return Platform.Windows;
        if (OperatingSystem.IsLinux())
            return Platform.Linux;
        if (OperatingSystem.IsMacOS())
            return Platform.MacOS;
        return Platform.Unknown;
    }

    /// <summary>
    /// Gets a human-readable platform name
    /// </summary>
    public static string GetPlatformName(Platform platform) => platform switch
    {
        Platform.Windows => "Windows",
        Platform.Linux => "Linux",
        Platform.MacOS => "macOS",
        _ => "Unknown"
    };
}

