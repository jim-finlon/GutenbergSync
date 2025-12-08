namespace GutenbergSync.Core.Extraction;

/// <summary>
/// Options for text extraction
/// </summary>
public sealed record TextExtractionOptions
{
    /// <summary>
    /// Strip Gutenberg headers and footers
    /// </summary>
    public bool StripHeaders { get; init; } = true;

    /// <summary>
    /// Normalize encoding to UTF-8
    /// </summary>
    public bool NormalizeEncoding { get; init; } = true;

    /// <summary>
    /// Chunk size in words
    /// </summary>
    public int ChunkSizeWords { get; init; } = 500;

    /// <summary>
    /// Chunk overlap in words
    /// </summary>
    public int ChunkOverlapWords { get; init; } = 50;

    /// <summary>
    /// Output format (json, parquet, arrow, txt)
    /// </summary>
    public string OutputFormat { get; init; } = "json";

    /// <summary>
    /// Compress output files
    /// </summary>
    public bool CompressOutput { get; init; } = false;

    /// <summary>
    /// Validate extracted chunks
    /// </summary>
    public bool ValidateChunks { get; init; } = true;

    /// <summary>
    /// Include full metadata in each chunk
    /// </summary>
    public bool IncludeMetadata { get; init; } = true;
}

