namespace GutenbergSync.Core.Sync;

/// <summary>
/// Options for sync orchestration
/// </summary>
public sealed record SyncOrchestrationOptions
{
    /// <summary>
    /// Target directory for archive storage
    /// </summary>
    public required string TargetDirectory { get; init; }

    /// <summary>
    /// Content preset name
    /// </summary>
    public string? Preset { get; init; }

    /// <summary>
    /// Sync only metadata (RDF files)
    /// </summary>
    public bool MetadataOnly { get; init; }

    /// <summary>
    /// Verify files after sync
    /// </summary>
    public bool VerifyAfterSync { get; init; } = true;

    /// <summary>
    /// Dry-run mode
    /// </summary>
    public bool DryRun { get; init; }
}

