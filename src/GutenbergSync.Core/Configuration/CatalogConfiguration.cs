namespace GutenbergSync.Core.Configuration;

/// <summary>
/// Configuration for catalog database
/// </summary>
public sealed record CatalogConfiguration
{
    /// <summary>
    /// Database file path (null = default to {targetDirectory}/gutenberg.db)
    /// </summary>
    public string? DatabasePath { get; init; }

    /// <summary>
    /// Automatically rebuild catalog on sync
    /// </summary>
    public bool AutoRebuildOnSync { get; init; } = true;

    /// <summary>
    /// Verify files after sync
    /// </summary>
    public bool VerifyAfterSync { get; init; } = true;

    /// <summary>
    /// Interval in days for audit scans
    /// </summary>
    public int AuditScanIntervalDays { get; init; } = 7;
}

