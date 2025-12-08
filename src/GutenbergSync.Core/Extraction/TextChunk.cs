namespace GutenbergSync.Core.Extraction;

/// <summary>
/// A chunk of text extracted from an ebook, ready for RAG ingestion
/// </summary>
public sealed record TextChunk
{
    /// <summary>
    /// Project Gutenberg book ID
    /// </summary>
    public required int BookId { get; init; }

    /// <summary>
    /// Book title
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// List of authors
    /// </summary>
    public IReadOnlyList<string> Authors { get; init; } = [];

    /// <summary>
    /// Language ISO code
    /// </summary>
    public string? LanguageIsoCode { get; init; }

    /// <summary>
    /// Publication date
    /// </summary>
    public DateOnly? PublicationDate { get; init; }

    /// <summary>
    /// List of subjects
    /// </summary>
    public IReadOnlyList<string> Subjects { get; init; } = [];

    /// <summary>
    /// Chunk index (0-based)
    /// </summary>
    public required int ChunkIndex { get; init; }

    /// <summary>
    /// Chunk text content
    /// </summary>
    public required string Text { get; init; }

    /// <summary>
    /// Word count of this chunk
    /// </summary>
    public int WordCount { get; init; }

    /// <summary>
    /// Character count of this chunk
    /// </summary>
    public int CharacterCount { get; init; }
}

