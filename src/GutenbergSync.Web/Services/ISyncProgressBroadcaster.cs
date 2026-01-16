using GutenbergSync.Core.Sync;

using GutenbergSync.Core.Sync;

namespace GutenbergSync.Web.Services;

public interface ISyncProgressBroadcaster
{
    Task BroadcastProgressAsync(SyncOrchestrationProgress progress);
    Task BroadcastCompleteAsync(SyncResult result);
    Task BroadcastErrorAsync(string error);
}

