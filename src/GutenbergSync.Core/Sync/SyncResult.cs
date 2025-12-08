namespace GutenbergSync.Core.Sync;

/// <summary>
/// Result of a sync operation
/// </summary>
public sealed record SyncResult
{
    /// <summary>
    /// Whether the sync was successful
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Exit code from rsync
    /// </summary>
    public int ExitCode { get; init; }

    /// <summary>
    /// Error message if sync failed
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Total files transferred
    /// </summary>
    public long FilesTransferred { get; init; }

    /// <summary>
    /// Total bytes transferred
    /// </summary>
    public long BytesTransferred { get; init; }

    /// <summary>
    /// Duration of the sync operation
    /// </summary>
    public TimeSpan Duration { get; init; }

    /// <summary>
    /// Whether the operation was cancelled (vs. failed due to error)
    /// </summary>
    public bool WasCancelled { get; init; }
}

