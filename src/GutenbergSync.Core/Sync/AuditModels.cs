namespace GutenbergSync.Core.Sync;

/// <summary>
/// Result of file verification
/// </summary>
public sealed record FileVerificationResult
{
    /// <summary>
    /// File path
    /// </summary>
    public required string FilePath { get; init; }

    /// <summary>
    /// Whether file exists
    /// </summary>
    public bool Exists { get; init; }

    /// <summary>
    /// Whether file is valid
    /// </summary>
    public bool IsValid { get; init; }

    /// <summary>
    /// Actual file size in bytes
    /// </summary>
    public long? ActualSize { get; init; }

    /// <summary>
    /// Expected file size in bytes
    /// </summary>
    public long? ExpectedSize { get; init; }

    /// <summary>
    /// Actual file checksum (SHA256)
    /// </summary>
    public string? ActualChecksum { get; init; }

    /// <summary>
    /// Expected file checksum
    /// </summary>
    public string? ExpectedChecksum { get; init; }

    /// <summary>
    /// Verification status
    /// </summary>
    public FileVerificationStatus Status { get; init; }

    /// <summary>
    /// Error message if verification failed
    /// </summary>
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// File verification status
/// </summary>
public enum FileVerificationStatus
{
    /// <summary>
    /// File verified successfully
    /// </summary>
    Valid,

    /// <summary>
    /// File is missing
    /// </summary>
    Missing,

    /// <summary>
    /// File size mismatch
    /// </summary>
    SizeMismatch,

    /// <summary>
    /// File checksum mismatch
    /// </summary>
    ChecksumMismatch,

    /// <summary>
    /// File is corrupt or unreadable
    /// </summary>
    Corrupt,

    /// <summary>
    /// Verification error occurred
    /// </summary>
    Error
}

/// <summary>
/// Result of directory audit scan
/// </summary>
public sealed record AuditScanResult
{
    /// <summary>
    /// Total files scanned
    /// </summary>
    public int TotalFiles { get; init; }

    /// <summary>
    /// Files that are valid
    /// </summary>
    public int ValidFiles { get; init; }

    /// <summary>
    /// Files that are missing
    /// </summary>
    public int MissingFiles { get; init; }

    /// <summary>
    /// Files that are corrupt
    /// </summary>
    public int CorruptFiles { get; init; }

    /// <summary>
    /// Files with size mismatches
    /// </summary>
    public int SizeMismatchFiles { get; init; }

    /// <summary>
    /// Files with checksum mismatches
    /// </summary>
    public int ChecksumMismatchFiles { get; init; }

    /// <summary>
    /// Detailed verification results
    /// </summary>
    public IReadOnlyList<FileVerificationResult> Results { get; init; } = [];

    /// <summary>
    /// Duration of scan
    /// </summary>
    public TimeSpan Duration { get; init; }
}

/// <summary>
/// Result of catalog verification
/// </summary>
public sealed record CatalogVerificationResult
{
    /// <summary>
    /// Total books in catalog
    /// </summary>
    public int TotalBooks { get; init; }

    /// <summary>
    /// Books with verified files
    /// </summary>
    public int VerifiedBooks { get; init; }

    /// <summary>
    /// Books with missing files
    /// </summary>
    public int MissingBooks { get; init; }

    /// <summary>
    /// Books with corrupt files
    /// </summary>
    public int CorruptBooks { get; init; }

    /// <summary>
    /// Detailed verification results
    /// </summary>
    public IReadOnlyList<FileVerificationResult> Results { get; init; } = [];

    /// <summary>
    /// Duration of verification
    /// </summary>
    public TimeSpan Duration { get; init; }
}

/// <summary>
/// Progress information for audit operations
/// </summary>
public sealed record AuditProgress
{
    /// <summary>
    /// Current file being verified
    /// </summary>
    public string? CurrentFile { get; init; }

    /// <summary>
    /// Files processed so far
    /// </summary>
    public int FilesProcessed { get; init; }

    /// <summary>
    /// Total files to process
    /// </summary>
    public int TotalFiles { get; init; }

    /// <summary>
    /// Progress percentage (0-100)
    /// </summary>
    public double ProgressPercent => TotalFiles > 0 ? (double)FilesProcessed / TotalFiles * 100 : 0;
}

