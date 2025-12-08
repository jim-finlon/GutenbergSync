namespace GutenbergSync.Core.Extraction;

/// <summary>
/// Complete metadata for a book (used in chunk exports)
/// </summary>
public sealed record BookMetadata
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
}

