namespace GutenbergSync.Core.Sync;

/// <summary>
/// Service for auditing and verifying file integrity
/// </summary>
public interface IAuditService
{
    /// <summary>
    /// Verifies a single file's integrity
    /// </summary>
    Task<FileVerificationResult> VerifyFileAsync(
        string filePath,
        long? expectedSize = null,
        string? expectedChecksum = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Scans directory for missing or corrupt files
    /// </summary>
    Task<AuditScanResult> ScanDirectoryAsync(
        string directoryPath,
        IProgress<AuditProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifies files against catalog metadata
    /// </summary>
    Task<CatalogVerificationResult> VerifyCatalogAsync(
        IProgress<AuditProgress>? progress = null,
        CancellationToken cancellationToken = default);
}

