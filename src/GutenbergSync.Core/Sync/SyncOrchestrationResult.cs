namespace GutenbergSync.Core.Sync;

/// <summary>
/// Result of sync orchestration
/// </summary>
public sealed record SyncOrchestrationResult
{
    /// <summary>
    /// Whether the sync was successful
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Metadata sync result
    /// </summary>
    public MetadataSyncResult? MetadataSync { get; init; }

    /// <summary>
    /// Content sync result
    /// </summary>
    public ContentSyncResult? ContentSync { get; init; }

    /// <summary>
    /// Total duration
    /// </summary>
    public TimeSpan Duration { get; init; }

    /// <summary>
    /// Error message if sync failed
    /// </summary>
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Result of metadata sync
/// </summary>
public sealed record MetadataSyncResult
{
    /// <summary>
    /// Whether the metadata sync was successful
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Number of RDF files synced
    /// </summary>
    public int RdfFilesSynced { get; init; }

    /// <summary>
    /// Number of metadata records added to catalog
    /// </summary>
    public int RecordsAdded { get; init; }

    /// <summary>
    /// Duration of metadata sync
    /// </summary>
    public TimeSpan Duration { get; init; }

    /// <summary>
    /// Error message if sync failed
    /// </summary>
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Result of content sync
/// </summary>
public sealed record ContentSyncResult
{
    /// <summary>
    /// Number of files synced
    /// </summary>
    public long FilesSynced { get; init; }

    /// <summary>
    /// Total bytes transferred
    /// </summary>
    public long BytesTransferred { get; init; }

    /// <summary>
    /// Duration of content sync
    /// </summary>
    public TimeSpan Duration { get; init; }
}

/// <summary>
/// Progress information for sync orchestration
/// </summary>
public sealed record SyncOrchestrationProgress
{
    /// <summary>
    /// Current phase (Metadata or Content)
    /// </summary>
    public string Phase { get; init; } = "";

    /// <summary>
    /// Progress message
    /// </summary>
    public string Message { get; init; } = "";

    /// <summary>
    /// Progress percentage (0-100)
    /// </summary>
    public double? ProgressPercent { get; init; }
}

