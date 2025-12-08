namespace GutenbergSync.Core.Extraction;

/// <summary>
/// Result of a text extraction operation
/// </summary>
public sealed record ExtractionResult
{
    /// <summary>
    /// Source file path
    /// </summary>
    public required string SourceFilePath { get; init; }

    /// <summary>
    /// Output file path
    /// </summary>
    public string? OutputFilePath { get; init; }

    /// <summary>
    /// Extracted text chunks
    /// </summary>
    public IReadOnlyList<TextChunk> Chunks { get; init; } = [];

    /// <summary>
    /// Book metadata
    /// </summary>
    public BookMetadata? BookMetadata { get; init; }

    /// <summary>
    /// Whether extraction was successful
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Error message if extraction failed
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Total word count
    /// </summary>
    public int TotalWordCount { get; init; }

    /// <summary>
    /// Total character count
    /// </summary>
    public int TotalCharacterCount { get; init; }
}

