namespace GutenbergSync.Core.Infrastructure;

/// <summary>
/// Service for discovering rsync binary availability
/// </summary>
public interface IRsyncDiscoveryService
{
    /// <summary>
    /// Discovers rsync binary on the current platform
    /// </summary>
    Task<RsyncDiscoveryResult> DiscoverAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets installation instructions for the specified platform
    /// </summary>
    string GetInstallationInstructions(Platform platform);
}

