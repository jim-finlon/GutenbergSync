namespace GutenbergSync.Core.Configuration;

/// <summary>
/// Configuration for text extraction
/// </summary>
public sealed record ExtractionConfiguration
{
    /// <summary>
    /// Default output directory for extracted text
    /// </summary>
    public string? OutputDirectory { get; init; }

    /// <summary>
    /// Strip Gutenberg headers/footers
    /// </summary>
    public bool StripHeaders { get; init; } = true;

    /// <summary>
    /// Normalize text encoding to UTF-8
    /// </summary>
    public bool NormalizeEncoding { get; init; } = true;

    /// <summary>
    /// Default chunk size in words
    /// </summary>
    public int DefaultChunkSizeWords { get; init; } = 500;

    /// <summary>
    /// Default chunk overlap in words
    /// </summary>
    public int DefaultChunkOverlapWords { get; init; } = 50;

    /// <summary>
    /// Enable incremental extraction by default
    /// </summary>
    public bool Incremental { get; init; } = true;

    /// <summary>
    /// Validate extracted chunks
    /// </summary>
    public bool ValidateChunks { get; init; } = true;

    /// <summary>
    /// Default output format (json, parquet, arrow)
    /// </summary>
    public string DefaultFormat { get; init; } = "json";

    /// <summary>
    /// Compress output files
    /// </summary>
    public bool CompressOutput { get; init; } = false;
}

