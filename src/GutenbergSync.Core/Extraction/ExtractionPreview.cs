namespace GutenbergSync.Core.Extraction;

/// <summary>
/// Preview of extraction results without processing files
/// </summary>
public sealed record ExtractionPreview
{
    /// <summary>
    /// Total files that would be processed
    /// </summary>
    public int TotalFiles { get; init; }

    /// <summary>
    /// Estimated total chunks
    /// </summary>
    public long EstimatedChunks { get; init; }

    /// <summary>
    /// Estimated output size in bytes
    /// </summary>
    public long EstimatedOutputSizeBytes { get; init; }

    /// <summary>
    /// Files that would be skipped (already extracted)
    /// </summary>
    public int SkippedFiles { get; init; }

    /// <summary>
    /// Files that would be processed
    /// </summary>
    public IReadOnlyList<string> FilesToProcess { get; init; } = [];
}

