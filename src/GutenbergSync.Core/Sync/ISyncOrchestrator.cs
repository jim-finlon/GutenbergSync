namespace GutenbergSync.Core.Sync;

/// <summary>
/// Orchestrates the metadata-first sync strategy
/// </summary>
public interface ISyncOrchestrator
{
    /// <summary>
    /// Performs a full sync using metadata-first strategy
    /// </summary>
    Task<SyncOrchestrationResult> SyncAsync(
        SyncOrchestrationOptions options,
        IProgress<SyncOrchestrationProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Syncs only metadata (RDF files) and builds catalog
    /// </summary>
    Task<MetadataSyncResult> SyncMetadataAsync(
        SyncOrchestrationOptions options,
        IProgress<SyncOrchestrationProgress>? progress = null,
        CancellationToken cancellationToken = default);
}

