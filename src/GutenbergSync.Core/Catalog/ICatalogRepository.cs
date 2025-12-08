using GutenbergSync.Core.Metadata;

namespace GutenbergSync.Core.Catalog;

/// <summary>
/// Repository for storing and querying ebook metadata
/// </summary>
public interface ICatalogRepository
{
    /// <summary>
    /// Initializes the database schema
    /// </summary>
    Task InitializeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Upserts a single ebook metadata record
    /// </summary>
    Task UpsertAsync(EbookMetadata metadata, CancellationToken cancellationToken = default);

    /// <summary>
    /// Upserts multiple ebook metadata records in a batch
    /// </summary>
    Task UpsertBatchAsync(IEnumerable<EbookMetadata> items, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets ebook metadata by book ID
    /// </summary>
    Task<EbookMetadata?> GetByIdAsync(int bookId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches for ebooks matching the search options
    /// </summary>
    Task<IReadOnlyList<EbookMetadata>> SearchAsync(CatalogSearchOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets catalog statistics
    /// </summary>
    Task<CatalogStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Exports catalog to CSV
    /// </summary>
    Task ExportToCsvAsync(string outputPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Exports catalog to JSON
    /// </summary>
    Task ExportToJsonAsync(string outputPath, CancellationToken cancellationToken = default);
}

