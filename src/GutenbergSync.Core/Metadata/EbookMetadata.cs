namespace GutenbergSync.Core.Metadata;

/// <summary>
/// Metadata for a Project Gutenberg ebook
/// </summary>
public sealed record EbookMetadata
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
    public IReadOnlyList<Author> Authors { get; init; } = [];

    /// <summary>
    /// Language name (e.g., "English")
    /// </summary>
    public string? Language { get; init; }

    /// <summary>
    /// Language ISO 639-1 code (e.g., "en")
    /// </summary>
    public string? LanguageIsoCode { get; init; }

    /// <summary>
    /// Publication date
    /// </summary>
    public DateOnly? PublicationDate { get; init; }

    /// <summary>
    /// List of subjects/topics
    /// </summary>
    public IReadOnlyList<string> Subjects { get; init; } = [];

    /// <summary>
    /// List of bookshelves/categories
    /// </summary>
    public IReadOnlyList<string> Bookshelves { get; init; } = [];

    /// <summary>
    /// Rights information
    /// </summary>
    public string? Rights { get; init; }

    /// <summary>
    /// Download count
    /// </summary>
    public int? DownloadCount { get; init; }

    /// <summary>
    /// RDF file path (relative to archive root)
    /// </summary>
    public string? RdfPath { get; init; }

    /// <summary>
    /// UTC timestamp when metadata was verified
    /// </summary>
    public DateTime? VerifiedUtc { get; init; }

    /// <summary>
    /// File checksum (SHA-256) for verification
    /// </summary>
    public string? Checksum { get; init; }

    /// <summary>
    /// Local file size in bytes (if file exists locally)
    /// </summary>
    public long? LocalFileSizeBytes { get; init; }
}

