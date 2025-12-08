namespace GutenbergSync.Core.Sync;

/// <summary>
/// Service for executing rsync operations
/// </summary>
public interface IRsyncService
{
    /// <summary>
    /// Synchronizes files from a remote endpoint to a local directory
    /// </summary>
    Task<SyncResult> SyncAsync(
        string endpoint,
        string targetDirectory,
        RsyncOptions options,
        IProgress<SyncProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if rsync is available
    /// </summary>
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a list of remote files (dry-run)
    /// </summary>
    Task<IReadOnlyList<RemoteFileInfo>> GetRemoteFileListAsync(
        string endpoint,
        string? pattern = null,
        CancellationToken cancellationToken = default);
}

