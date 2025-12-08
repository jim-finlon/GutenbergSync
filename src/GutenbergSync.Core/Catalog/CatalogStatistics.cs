namespace GutenbergSync.Core.Catalog;

/// <summary>
/// Statistics about the catalog
/// </summary>
public sealed record CatalogStatistics
{
    /// <summary>
    /// Total number of ebooks
    /// </summary>
    public int TotalBooks { get; init; }

    /// <summary>
    /// Total number of authors
    /// </summary>
    public int TotalAuthors { get; init; }

    /// <summary>
    /// Number of unique languages
    /// </summary>
    public int UniqueLanguages { get; init; }

    /// <summary>
    /// Number of unique subjects
    /// </summary>
    public int UniqueSubjects { get; init; }

    /// <summary>
    /// Total size of all files in bytes
    /// </summary>
    public long TotalFileSizeBytes { get; init; }

    /// <summary>
    /// Date range of publications
    /// </summary>
    public DateRange? PublicationDateRange { get; init; }

    /// <summary>
    /// Book ID range
    /// </summary>
    public IdRange? BookIdRange { get; init; }
}

