using GutenbergSync.Core.Sync;
using GutenbergSync.Core.Configuration;
using GutenbergSync.Web.Services;

namespace GutenbergSync.Web.Services;

public class WebSyncService
{
    private readonly ISyncOrchestrator _orchestrator;
    private readonly ISyncProgressBroadcaster _broadcaster;
    private readonly AppConfiguration _config;
    private readonly ILogger<WebSyncService> _logger;

    public WebSyncService(
        ISyncOrchestrator orchestrator,
        ISyncProgressBroadcaster broadcaster,
        AppConfiguration config,
        ILogger<WebSyncService> logger)
    {
        _orchestrator = orchestrator;
        _broadcaster = broadcaster;
        _config = config;
        _logger = logger;
    }

    public async Task<SyncOrchestrationResult> StartSyncAsync(CancellationToken cancellationToken = default)
    {
        var options = new SyncOrchestrationOptions
        {
            TargetDirectory = _config.Sync.TargetDirectory,
            Preset = _config.Sync.Preset,
            TimeoutSeconds = _config.Sync.TimeoutSeconds
        };

        var progressHandler = new Progress<SyncOrchestrationProgress>(progress =>
        {
            // Broadcast the progress directly - the frontend can handle SyncOrchestrationProgress format
            _broadcaster.BroadcastProgressAsync(progress).Wait();
        });

        try
        {
            var result = await _orchestrator.SyncAsync(options, progressHandler, cancellationToken);
            // Convert SyncOrchestrationResult to SyncResult for broadcasting
            var syncResult = new SyncResult
            {
                Success = result.Success,
                FilesTransferred = result.ContentSync?.FilesSynced ?? 0,
                BytesTransferred = result.ContentSync?.BytesTransferred ?? 0,
                Duration = (result.MetadataSync?.Duration ?? TimeSpan.Zero) + (result.ContentSync?.Duration ?? TimeSpan.Zero)
            };
            await _broadcaster.BroadcastCompleteAsync(syncResult);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Sync failed");
            await _broadcaster.BroadcastErrorAsync(ex.Message);
            throw;
        }
    }
}

