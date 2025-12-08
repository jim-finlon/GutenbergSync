namespace GutenbergSync.Core.Configuration;

/// <summary>
/// Configuration for a Project Gutenberg mirror endpoint
/// </summary>
public sealed record MirrorEndpoint
{
    /// <summary>
    /// Host name of the mirror
    /// </summary>
    public required string Host { get; init; }

    /// <summary>
    /// Module name (gutenberg, gutenberg-epub)
    /// </summary>
    public required string Module { get; init; }

    /// <summary>
    /// Priority (lower = higher priority)
    /// </summary>
    public int Priority { get; init; } = 1;

    /// <summary>
    /// Geographic region
    /// </summary>
    public string? Region { get; init; }

    /// <summary>
    /// Gets the rsync endpoint string (host::module)
    /// </summary>
    public string GetEndpointString() => $"{Host}::{Module}";
}

