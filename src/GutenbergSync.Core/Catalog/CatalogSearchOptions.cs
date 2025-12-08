namespace GutenbergSync.Core.Catalog;

/// <summary>
/// Options for searching the catalog
/// </summary>
public sealed record CatalogSearchOptions
{
    /// <summary>
    /// Search query (searches title and author)
    /// </summary>
    public string? Query { get; init; }

    /// <summary>
    /// Filter by author name
    /// </summary>
    public string? Author { get; init; }

    /// <summary>
    /// Filter by subject
    /// </summary>
    public string? Subject { get; init; }

    /// <summary>
    /// Filter by language (name or ISO code)
    /// </summary>
    public string? Language { get; init; }

    /// <summary>
    /// Filter by publication date range
    /// </summary>
    public DateRange? PublicationDateRange { get; init; }

    /// <summary>
    /// Filter by book ID range
    /// </summary>
    public IdRange? BookIdRange { get; init; }

    /// <summary>
    /// Maximum number of results
    /// </summary>
    public int? Limit { get; init; }

    /// <summary>
    /// Offset for pagination
    /// </summary>
    public int Offset { get; init; }
}

/// <summary>
/// Date range filter
/// </summary>
public sealed record DateRange
{
    public DateOnly? Start { get; init; }
    public DateOnly? End { get; init; }
}

/// <summary>
/// ID range filter
/// </summary>
public sealed record IdRange
{
    public int? Start { get; init; }
    public int? End { get; init; }
}

