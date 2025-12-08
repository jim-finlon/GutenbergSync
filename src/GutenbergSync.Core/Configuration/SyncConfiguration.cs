namespace GutenbergSync.Core.Configuration;

/// <summary>
/// Configuration for synchronization operations
/// </summary>
public sealed record SyncConfiguration
{
    /// <summary>
    /// Target directory for archive storage
    /// </summary>
    public required string TargetDirectory { get; init; }

    /// <summary>
    /// Content preset name (text-only, text-epub, all-text, full)
    /// </summary>
    public string? Preset { get; init; }

    /// <summary>
    /// Mirror endpoints with priorities
    /// </summary>
    public IReadOnlyList<MirrorEndpoint> Mirrors { get; init; } = [];

    /// <summary>
    /// File patterns to include
    /// </summary>
    public IReadOnlyList<string> Include { get; init; } = [];

    /// <summary>
    /// File patterns to exclude
    /// </summary>
    public IReadOnlyList<string> Exclude { get; init; } = [];

    /// <summary>
    /// Maximum file size in MB
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
    /// Timeout in seconds for rsync operations
    /// </summary>
    public int TimeoutSeconds { get; init; } = 600;
}

