namespace GutenbergSync.Core.Extraction;

/// <summary>
/// Progress information for batch extraction
/// </summary>
public sealed record BatchProgress
{
    /// <summary>
    /// Total files to process
    /// </summary>
    public int TotalFiles { get; init; }

    /// <summary>
    /// Files processed so far
    /// </summary>
    public int FilesProcessed { get; init; }

    /// <summary>
    /// Current file being processed
    /// </summary>
    public string? CurrentFile { get; init; }

    /// <summary>
    /// Total chunks created
    /// </summary>
    public long TotalChunks { get; init; }

    /// <summary>
    /// Progress percentage (0-100)
    /// </summary>
    public double ProgressPercent => TotalFiles > 0 ? (double)FilesProcessed / TotalFiles * 100 : 0;
}

