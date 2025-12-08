namespace GutenbergSync.Core.Extraction;

/// <summary>
/// Extracts and processes text from Project Gutenberg files
/// </summary>
public interface ITextExtractor
{
    /// <summary>
    /// Extracts text from a source file
    /// </summary>
    Task<ExtractionResult> ExtractAsync(
        string sourceFilePath,
        TextExtractionOptions options,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Extracts text from multiple files in batch
    /// </summary>
    Task ExtractBatchAsync(
        IEnumerable<string> sourceFiles,
        string outputDirectory,
        TextExtractionOptions options,
        IProgress<BatchProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Previews extraction without processing files
    /// </summary>
    Task<ExtractionPreview> PreviewExtractionAsync(
        IEnumerable<string> sourceFiles,
        TextExtractionOptions options,
        CancellationToken cancellationToken = default);
}

