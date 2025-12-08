namespace GutenbergSync.Core.Sync;

/// <summary>
/// Options for rsync operations
/// </summary>
public sealed record RsyncOptions
{
    /// <summary>
    /// File patterns to include
    /// </summary>
    public IReadOnlyList<string> Include { get; init; } = [];

    /// <summary>
    /// File patterns to exclude
    /// </summary>
    public IReadOnlyList<string> Exclude { get; init; } = [];

    /// <summary>
    /// Maximum file size in MB (null = no limit)
    /// </summary>
    public int? MaxFileSizeMb { get; init; }

    /// <summary>
    /// Bandwidth limit in KB/s (null = no limit)
    /// </summary>
    public int? BandwidthLimitKbps { get; init; }

    /// <summary>
    /// Delete local files not present on server
    /// </summary>
    public bool DeleteRemoved { get; init; }

    /// <summary>
    /// Dry-run mode (don't actually transfer files)
    /// </summary>
    public bool DryRun { get; init; }

    /// <summary>
    /// Verbose output
    /// </summary>
    public bool Verbose { get; init; }

    /// <summary>
    /// Show progress
    /// </summary>
    public bool ShowProgress { get; init; } = true;

    /// <summary>
    /// Timeout in seconds
    /// </summary>
    public int TimeoutSeconds { get; init; } = 600;
}

