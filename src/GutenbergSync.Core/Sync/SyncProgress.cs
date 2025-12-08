namespace GutenbergSync.Core.Sync;

/// <summary>
/// Progress information for a sync operation
/// </summary>
public sealed record SyncProgress
{
    /// <summary>
    /// Total files to transfer
    /// </summary>
    public long? TotalFiles { get; init; }

    /// <summary>
    /// Files transferred so far
    /// </summary>
    public long FilesTransferred { get; init; }

    /// <summary>
    /// Total bytes to transfer
    /// </summary>
    public long? TotalBytes { get; init; }

    /// <summary>
    /// Bytes transferred so far
    /// </summary>
    public long BytesTransferred { get; init; }

    /// <summary>
    /// Current file being transferred
    /// </summary>
    public string? CurrentFile { get; init; }

    /// <summary>
    /// Transfer speed in bytes per second
    /// </summary>
    public long? SpeedBytesPerSecond { get; init; }

    /// <summary>
    /// Estimated time remaining in seconds
    /// </summary>
    public int? EstimatedSecondsRemaining { get; init; }

    /// <summary>
    /// Progress percentage (0-100)
    /// </summary>
    public double? ProgressPercent => TotalBytes.HasValue && TotalBytes.Value > 0
        ? (double)BytesTransferred / TotalBytes.Value * 100
        : null;
}

