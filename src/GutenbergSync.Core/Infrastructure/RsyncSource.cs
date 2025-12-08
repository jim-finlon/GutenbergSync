namespace GutenbergSync.Core.Infrastructure;

/// <summary>
/// Source where rsync was discovered
/// </summary>
public enum RsyncSource
{
    /// <summary>
    /// Not found
    /// </summary>
    NotFound,

    /// <summary>
    /// Found in system PATH
    /// </summary>
    Path,

    /// <summary>
    /// Native installation (Linux/macOS)
    /// </summary>
    Native,

    /// <summary>
    /// Windows Subsystem for Linux
    /// </summary>
    WSL,

    /// <summary>
    /// Cygwin installation
    /// </summary>
    Cygwin
}

