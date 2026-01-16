using GutenbergSync.Core.Sync;
using GutenbergSync.Web.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace GutenbergSync.Web.Services;

public class SyncProgressBroadcaster : ISyncProgressBroadcaster
{
    private readonly IHubContext<SyncProgressHub> _hubContext;

    public SyncProgressBroadcaster(IHubContext<SyncProgressHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task BroadcastProgressAsync(SyncOrchestrationProgress progress)
    {
        await _hubContext.Clients.All.SendAsync("ProgressUpdate", progress);
    }

    public async Task BroadcastCompleteAsync(SyncResult result)
    {
        await _hubContext.Clients.All.SendAsync("SyncComplete", result);
    }

    public async Task BroadcastErrorAsync(string error)
    {
        await _hubContext.Clients.All.SendAsync("SyncError", error);
    }
}

