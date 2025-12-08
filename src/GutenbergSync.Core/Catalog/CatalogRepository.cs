using System.Data;
using System.Text;
using Dapper;
using Microsoft.Data.Sqlite;
using GutenbergSync.Core.Configuration;
using GutenbergSync.Core.Metadata;
using Serilog;

namespace GutenbergSync.Core.Catalog;

/// <summary>
/// Repository for storing and querying ebook metadata in SQLite
/// </summary>
public sealed class CatalogRepository : ICatalogRepository
{
    private readonly string _connectionString;
    private readonly ILogger _logger;

    public CatalogRepository(AppConfiguration config, ILogger logger)
    {
        _logger = logger;
        
        // Resolve database path
        var dbPath = config.Catalog.DatabasePath;
        if (string.IsNullOrWhiteSpace(dbPath))
        {
            dbPath = Path.Combine(config.Sync.TargetDirectory, "gutenberg.db");
        }

        // Ensure directory exists
        var dbDir = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrWhiteSpace(dbDir) && !Directory.Exists(dbDir))
        {
            Directory.CreateDirectory(dbDir);
        }

        _connectionString = $"Data Source={dbPath};";
        _logger.Information("Catalog database: {DbPath}", dbPath);
    }

    /// <inheritdoc/>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        // Enable foreign keys
        await connection.ExecuteAsync("PRAGMA foreign_keys = ON;");

        // Create tables
        await CreateTablesAsync(connection, cancellationToken);

        // Create indexes
        await CreateIndexesAsync(connection, cancellationToken);

        // Create FTS5 virtual table for full-text search
        await CreateFullTextSearchAsync(connection, cancellationToken);

        _logger.Information("Catalog database initialized");
    }

    /// <inheritdoc/>
    public async Task UpsertAsync(EbookMetadata metadata, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            // Upsert ebook
            await UpsertEbookAsync(connection, metadata, cancellationToken);

            // Upsert authors
            await UpsertAuthorsAsync(connection, metadata, cancellationToken);

            // Upsert subjects
            await UpsertSubjectsAsync(connection, metadata, cancellationToken);

            // Upsert bookshelves
            await UpsertBookshelvesAsync(connection, metadata, cancellationToken);

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task UpsertBatchAsync(IEnumerable<EbookMetadata> items, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            foreach (var metadata in items)
            {
                await UpsertEbookAsync(connection, metadata, cancellationToken);
                await UpsertAuthorsAsync(connection, metadata, cancellationToken);
                await UpsertSubjectsAsync(connection, metadata, cancellationToken);
                await UpsertBookshelvesAsync(connection, metadata, cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            _logger.Information("Upserted {Count} ebooks in batch", items.Count());
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<EbookMetadata?> GetByIdAsync(int bookId, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var ebook = await connection.QueryFirstOrDefaultAsync<EbookRecord>(
            @"SELECT * FROM ebooks WHERE book_id = @BookId",
            new { BookId = bookId });

        if (ebook == null)
            return null;

        var authors = await connection.QueryAsync<AuthorRecord>(
            @"SELECT a.* FROM authors a
              INNER JOIN ebook_authors ea ON a.id = ea.author_id
              WHERE ea.ebook_id = @BookId",
            new { BookId = bookId });

        var subjects = await connection.QueryAsync<string>(
            @"SELECT subject FROM ebook_subjects WHERE ebook_id = @BookId",
            new { BookId = bookId });

        var bookshelves = await connection.QueryAsync<string>(
            @"SELECT bookshelf FROM ebook_bookshelves WHERE ebook_id = @BookId",
            new { BookId = bookId });

        return MapToEbookMetadata(ebook, authors, subjects, bookshelves);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<EbookMetadata>> SearchAsync(CatalogSearchOptions options, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var query = new StringBuilder("SELECT DISTINCT e.* FROM ebooks e");
        var conditions = new List<string>();
        var parameters = new DynamicParameters();

        // Build WHERE clause
        if (!string.IsNullOrWhiteSpace(options.Query))
        {
            query.Append(" INNER JOIN ebooks_fts fts ON e.book_id = fts.book_id");
            conditions.Add("ebooks_fts MATCH @Query");
            parameters.Add("Query", options.Query);
        }

        if (!string.IsNullOrWhiteSpace(options.Author))
        {
            query.Append(" INNER JOIN ebook_authors ea ON e.book_id = ea.ebook_id");
            query.Append(" INNER JOIN authors a ON ea.author_id = a.id");
            conditions.Add("a.name LIKE @Author");
            parameters.Add("Author", $"%{options.Author}%");
        }

        if (!string.IsNullOrWhiteSpace(options.Subject))
        {
            query.Append(" INNER JOIN ebook_subjects es ON e.book_id = es.ebook_id");
            conditions.Add("es.subject LIKE @Subject");
            parameters.Add("Subject", $"%{options.Subject}%");
        }

        if (!string.IsNullOrWhiteSpace(options.Language))
        {
            conditions.Add("(e.language LIKE @Language OR e.language_iso_code = @Language)");
            parameters.Add("Language", options.Language);
        }

        if (options.PublicationDateRange != null)
        {
            if (options.PublicationDateRange.Start.HasValue)
            {
                conditions.Add("e.publication_date >= @StartDate");
                parameters.Add("StartDate", options.PublicationDateRange.Start.Value.ToString("yyyy-MM-dd"));
            }
            if (options.PublicationDateRange.End.HasValue)
            {
                conditions.Add("e.publication_date <= @EndDate");
                parameters.Add("EndDate", options.PublicationDateRange.End.Value.ToString("yyyy-MM-dd"));
            }
        }

        if (options.BookIdRange != null)
        {
            if (options.BookIdRange.Start.HasValue)
            {
                conditions.Add("e.book_id >= @StartId");
                parameters.Add("StartId", options.BookIdRange.Start.Value);
            }
            if (options.BookIdRange.End.HasValue)
            {
                conditions.Add("e.book_id <= @EndId");
                parameters.Add("EndId", options.BookIdRange.End.Value);
            }
        }

        if (conditions.Count > 0)
        {
            query.Append(" WHERE ").Append(string.Join(" AND ", conditions));
        }

        query.Append(" ORDER BY e.book_id");

        if (options.Limit.HasValue)
        {
            query.Append(" LIMIT @Limit");
            parameters.Add("Limit", options.Limit.Value);
        }

        if (options.Offset > 0)
        {
            query.Append(" OFFSET @Offset");
            parameters.Add("Offset", options.Offset);
        }

        var ebooks = await connection.QueryAsync<EbookRecord>(query.ToString(), parameters);

        // Load related data for each ebook
        var results = new List<EbookMetadata>();
        foreach (var ebook in ebooks)
        {
            var metadata = await GetByIdAsync(ebook.BookId, cancellationToken);
            if (metadata != null)
            {
                results.Add(metadata);
            }
        }

        return results;
    }

    /// <inheritdoc/>
    public async Task<CatalogStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var totalBooks = await connection.QuerySingleAsync<int>("SELECT COUNT(*) FROM ebooks");
        var totalAuthors = await connection.QuerySingleAsync<int>("SELECT COUNT(DISTINCT id) FROM authors");
        var uniqueLanguages = await connection.QuerySingleAsync<int>("SELECT COUNT(DISTINCT language_iso_code) FROM ebooks WHERE language_iso_code IS NOT NULL");
        var uniqueSubjects = await connection.QuerySingleAsync<int>("SELECT COUNT(DISTINCT subject) FROM ebook_subjects");
        var totalFileSize = await connection.QuerySingleAsync<long>("SELECT COALESCE(SUM(local_file_size_bytes), 0) FROM ebooks");

        var dateRange = await connection.QueryFirstOrDefaultAsync<(DateOnly? Min, DateOnly? Max)>(
            @"SELECT MIN(publication_date) as Min, MAX(publication_date) as Max FROM ebooks WHERE publication_date IS NOT NULL");

        var idRange = await connection.QueryFirstOrDefaultAsync<(int? Min, int? Max)>(
            @"SELECT MIN(book_id) as Min, MAX(book_id) as Max FROM ebooks");

        return new CatalogStatistics
        {
            TotalBooks = totalBooks,
            TotalAuthors = totalAuthors,
            UniqueLanguages = uniqueLanguages,
            UniqueSubjects = uniqueSubjects,
            TotalFileSizeBytes = totalFileSize,
            PublicationDateRange = dateRange.Min.HasValue || dateRange.Max.HasValue
                ? new DateRange { Start = dateRange.Min, End = dateRange.Max }
                : null,
            BookIdRange = idRange.Min.HasValue || idRange.Max.HasValue
                ? new IdRange { Start = idRange.Min, End = idRange.Max }
                : null
        };
    }

    /// <inheritdoc/>
    public async Task ExportToCsvAsync(string outputPath, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var ebooks = await connection.QueryAsync<EbookRecord>("SELECT * FROM ebooks ORDER BY book_id");

        await using var writer = new StreamWriter(outputPath, false, Encoding.UTF8);
        
        // Write header
        await writer.WriteLineAsync("BookId,Title,Language,LanguageIsoCode,PublicationDate,Subjects,Authors,Rights,DownloadCount");

        foreach (var ebook in ebooks)
        {
            var authors = await connection.QueryAsync<string>(
                @"SELECT a.name FROM authors a
                  INNER JOIN ebook_authors ea ON a.id = ea.author_id
                  WHERE ea.ebook_id = @BookId",
                new { BookId = ebook.BookId });

            var subjects = await connection.QueryAsync<string>(
                @"SELECT subject FROM ebook_subjects WHERE ebook_id = @BookId",
                new { BookId = ebook.BookId });

            var line = $"{ebook.BookId},\"{EscapeCsv(ebook.Title)}\",\"{EscapeCsv(ebook.Language ?? "")}\",\"{EscapeCsv(ebook.LanguageIsoCode ?? "")}\",\"{ebook.PublicationDate?.ToString("yyyy-MM-dd") ?? ""}\",\"{EscapeCsv(string.Join("; ", subjects))}\",\"{EscapeCsv(string.Join("; ", authors))}\",\"{EscapeCsv(ebook.Rights ?? "")}\",{ebook.DownloadCount ?? 0}";
            await writer.WriteLineAsync(line);
        }

        _logger.Information("Exported catalog to CSV: {Path}", outputPath);
    }

    /// <inheritdoc/>
    public async Task ExportToJsonAsync(string outputPath, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var ebooks = await connection.QueryAsync<EbookRecord>("SELECT * FROM ebooks ORDER BY book_id");

        var exportData = new List<object>();

        foreach (var ebook in ebooks)
        {
            var metadata = await GetByIdAsync(ebook.BookId, cancellationToken);
            if (metadata != null)
            {
                exportData.Add(new
                {
                    metadata.BookId,
                    metadata.Title,
                    Authors = metadata.Authors.Select(a => a.Name).ToList(),
                    metadata.Language,
                    metadata.LanguageIsoCode,
                    PublicationDate = metadata.PublicationDate?.ToString("yyyy-MM-dd"),
                    metadata.Subjects,
                    metadata.Bookshelves,
                    metadata.Rights,
                    metadata.DownloadCount
                });
            }
        }

        var json = System.Text.Json.JsonSerializer.Serialize(exportData, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true
        });

        await File.WriteAllTextAsync(outputPath, json, cancellationToken);
        _logger.Information("Exported catalog to JSON: {Path}", outputPath);
    }

    private static async Task CreateTablesAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        var sql = @"
            CREATE TABLE IF NOT EXISTS ebooks (
                book_id INTEGER PRIMARY KEY,
                title TEXT NOT NULL,
                language TEXT,
                language_iso_code TEXT,
                publication_date TEXT,
                rights TEXT,
                download_count INTEGER,
                rdf_path TEXT,
                verified_utc TEXT,
                checksum TEXT,
                created_utc TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
                updated_utc TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
            );

            CREATE TABLE IF NOT EXISTS authors (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                name TEXT NOT NULL,
                birth_year INTEGER,
                death_year INTEGER,
                web_page TEXT,
                UNIQUE(name)
            );

            CREATE TABLE IF NOT EXISTS ebook_authors (
                ebook_id INTEGER NOT NULL REFERENCES ebooks(book_id) ON DELETE CASCADE,
                author_id INTEGER NOT NULL REFERENCES authors(id) ON DELETE CASCADE,
                PRIMARY KEY (ebook_id, author_id)
            );

            CREATE TABLE IF NOT EXISTS ebook_subjects (
                ebook_id INTEGER NOT NULL REFERENCES ebooks(book_id) ON DELETE CASCADE,
                subject TEXT NOT NULL,
                PRIMARY KEY (ebook_id, subject)
            );

            CREATE TABLE IF NOT EXISTS ebook_bookshelves (
                ebook_id INTEGER NOT NULL REFERENCES ebooks(book_id) ON DELETE CASCADE,
                bookshelf TEXT NOT NULL,
                PRIMARY KEY (ebook_id, bookshelf)
            );
        ";

        await connection.ExecuteAsync(sql);
    }

    private static async Task CreateIndexesAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        var sql = @"
            CREATE INDEX IF NOT EXISTS idx_ebooks_language ON ebooks(language);
            CREATE INDEX IF NOT EXISTS idx_ebooks_language_iso ON ebooks(language_iso_code);
            CREATE INDEX IF NOT EXISTS idx_ebooks_publication_date ON ebooks(publication_date);
            CREATE INDEX IF NOT EXISTS idx_authors_name ON authors(name);
        ";

        await connection.ExecuteAsync(sql);
    }

    private static async Task CreateFullTextSearchAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        // Create FTS5 virtual table for full-text search
        var sql = @"
            CREATE VIRTUAL TABLE IF NOT EXISTS ebooks_fts USING fts5(
                book_id UNINDEXED,
                title,
                content='ebooks',
                content_rowid='book_id'
            );

            CREATE TRIGGER IF NOT EXISTS ebooks_fts_insert AFTER INSERT ON ebooks BEGIN
                INSERT INTO ebooks_fts(book_id, title) VALUES (new.book_id, new.title);
            END;

            CREATE TRIGGER IF NOT EXISTS ebooks_fts_delete AFTER DELETE ON ebooks BEGIN
                DELETE FROM ebooks_fts WHERE book_id = old.book_id;
            END;

            CREATE TRIGGER IF NOT EXISTS ebooks_fts_update AFTER UPDATE ON ebooks BEGIN
                DELETE FROM ebooks_fts WHERE book_id = old.book_id;
                INSERT INTO ebooks_fts(book_id, title) VALUES (new.book_id, new.title);
            END;
        ";

        await connection.ExecuteAsync(sql);
    }

    private static async Task UpsertEbookAsync(SqliteConnection connection, EbookMetadata metadata, CancellationToken cancellationToken)
    {
        var sql = @"
            INSERT INTO ebooks (book_id, title, language, language_iso_code, publication_date, rights, download_count, rdf_path, verified_utc, checksum, updated_utc)
            VALUES (@BookId, @Title, @Language, @LanguageIsoCode, @PublicationDate, @Rights, @DownloadCount, @RdfPath, @VerifiedUtc, @Checksum, CURRENT_TIMESTAMP)
            ON CONFLICT(book_id) DO UPDATE SET
                title = excluded.title,
                language = excluded.language,
                language_iso_code = excluded.language_iso_code,
                publication_date = excluded.publication_date,
                rights = excluded.rights,
                download_count = excluded.download_count,
                rdf_path = excluded.rdf_path,
                verified_utc = excluded.verified_utc,
                checksum = excluded.checksum,
                updated_utc = CURRENT_TIMESTAMP
        ";

        await connection.ExecuteAsync(sql, new
        {
            BookId = metadata.BookId,
            Title = metadata.Title,
            Language = metadata.Language,
            LanguageIsoCode = metadata.LanguageIsoCode,
            PublicationDate = metadata.PublicationDate?.ToString("yyyy-MM-dd"),
            Rights = metadata.Rights,
            DownloadCount = metadata.DownloadCount,
            RdfPath = metadata.RdfPath,
            VerifiedUtc = metadata.VerifiedUtc?.ToString("yyyy-MM-dd HH:mm:ss"),
            Checksum = metadata.Checksum
        });
    }

    private static async Task UpsertAuthorsAsync(SqliteConnection connection, EbookMetadata metadata, CancellationToken cancellationToken)
    {
        // Delete existing associations
        await connection.ExecuteAsync(
            "DELETE FROM ebook_authors WHERE ebook_id = @BookId",
            new { BookId = metadata.BookId });

        foreach (var author in metadata.Authors)
        {
            // Insert or get author
            var authorId = await connection.QuerySingleOrDefaultAsync<int?>(
                "SELECT id FROM authors WHERE name = @Name",
                new { Name = author.Name });

            if (!authorId.HasValue)
            {
                authorId = await connection.QuerySingleAsync<int>(
                    @"INSERT INTO authors (name, birth_year, death_year, web_page)
                      VALUES (@Name, @BirthYear, @DeathYear, @WebPage)
                      RETURNING id",
                    new
                    {
                        Name = author.Name,
                        BirthYear = author.BirthYear,
                        DeathYear = author.DeathYear,
                        WebPage = author.WebPage
                    });
            }

            // Associate with ebook
            await connection.ExecuteAsync(
                "INSERT OR IGNORE INTO ebook_authors (ebook_id, author_id) VALUES (@BookId, @AuthorId)",
                new { BookId = metadata.BookId, AuthorId = authorId.Value });
        }
    }

    private static async Task UpsertSubjectsAsync(SqliteConnection connection, EbookMetadata metadata, CancellationToken cancellationToken)
    {
        await connection.ExecuteAsync(
            "DELETE FROM ebook_subjects WHERE ebook_id = @BookId",
            new { BookId = metadata.BookId });

        foreach (var subject in metadata.Subjects)
        {
            await connection.ExecuteAsync(
                "INSERT INTO ebook_subjects (ebook_id, subject) VALUES (@BookId, @Subject)",
                new { BookId = metadata.BookId, Subject = subject });
        }
    }

    private static async Task UpsertBookshelvesAsync(SqliteConnection connection, EbookMetadata metadata, CancellationToken cancellationToken)
    {
        await connection.ExecuteAsync(
            "DELETE FROM ebook_bookshelves WHERE ebook_id = @BookId",
            new { BookId = metadata.BookId });

        foreach (var bookshelf in metadata.Bookshelves)
        {
            await connection.ExecuteAsync(
                "INSERT INTO ebook_bookshelves (ebook_id, bookshelf) VALUES (@BookId, @Bookshelf)",
                new { BookId = metadata.BookId, Bookshelf = bookshelf });
        }
    }

    private static EbookMetadata MapToEbookMetadata(EbookRecord ebook, IEnumerable<AuthorRecord> authors, IEnumerable<string> subjects, IEnumerable<string> bookshelves)
    {
        return new EbookMetadata
        {
            BookId = ebook.BookId,
            Title = ebook.Title,
            Authors = authors.Select(a => new Author
            {
                Name = a.Name,
                BirthYear = a.BirthYear,
                DeathYear = a.DeathYear,
                WebPage = a.WebPage
            }).ToList(),
            Language = ebook.Language,
            LanguageIsoCode = ebook.LanguageIsoCode,
            PublicationDate = ebook.PublicationDate != null && DateOnly.TryParse(ebook.PublicationDate, out var date) ? date : null,
            Subjects = subjects.ToList(),
            Bookshelves = bookshelves.ToList(),
            Rights = ebook.Rights,
            DownloadCount = ebook.DownloadCount,
            RdfPath = ebook.RdfPath,
            VerifiedUtc = ebook.VerifiedUtc != null && DateTime.TryParse(ebook.VerifiedUtc, null, System.Globalization.DateTimeStyles.None, out var verified) ? verified : null,
            Checksum = ebook.Checksum
        };
    }

    private static string EscapeCsv(string value)
    {
        if (string.IsNullOrEmpty(value))
            return "";

        if (value.Contains('"') || value.Contains(',') || value.Contains('\n'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        return value;
    }

    // Internal records for Dapper mapping
    private sealed record EbookRecord
    {
        public int BookId { get; init; }
        public string Title { get; init; } = "";
        public string? Language { get; init; }
        public string? LanguageIsoCode { get; init; }
        public string? PublicationDate { get; init; }
        public string? Rights { get; init; }
        public int? DownloadCount { get; init; }
        public string? RdfPath { get; init; }
        public string? VerifiedUtc { get; init; }
        public string? Checksum { get; init; }
    }

    private sealed record AuthorRecord
    {
        public int Id { get; init; }
        public string Name { get; init; } = "";
        public int? BirthYear { get; init; }
        public int? DeathYear { get; init; }
        public string? WebPage { get; init; }
    }
}

