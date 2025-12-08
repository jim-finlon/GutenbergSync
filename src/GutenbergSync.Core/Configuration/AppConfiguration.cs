namespace GutenbergSync.Core.Configuration;

/// <summary>
/// Root application configuration
/// </summary>
public sealed record AppConfiguration
{
    /// <summary>
    /// Synchronization configuration
    /// </summary>
    public required SyncConfiguration Sync { get; init; }

    /// <summary>
    /// Catalog configuration
    /// </summary>
    public CatalogConfiguration Catalog { get; init; } = new();

    /// <summary>
    /// Extraction configuration
    /// </summary>
    public ExtractionConfiguration Extraction { get; init; } = new();

    /// <summary>
    /// Logging configuration
    /// </summary>
    public LoggingConfiguration Logging { get; init; } = new();
}

