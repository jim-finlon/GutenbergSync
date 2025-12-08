namespace GutenbergSync.Core.Infrastructure;

/// <summary>
/// Result of rsync discovery
/// </summary>
public sealed record RsyncDiscoveryResult
{
    /// <summary>
    /// Whether rsync is available
    /// </summary>
    public bool IsAvailable { get; init; }

    /// <summary>
    /// Path to rsync executable
    /// </summary>
    public string? ExecutablePath { get; init; }

    /// <summary>
    /// Detected platform
    /// </summary>
    public Platform Platform { get; init; }

    /// <summary>
    /// Source where rsync was found
    /// </summary>
    public RsyncSource Source { get; init; }

    /// <summary>
    /// rsync version string
    /// </summary>
    public string? Version { get; init; }

    /// <summary>
    /// Installation instructions if not available
    /// </summary>
    public string? InstallationInstructions { get; init; }
}

