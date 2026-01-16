using System.Data;
using System.Linq;
using System.Text;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using GutenbergSync.Core.Configuration;
using GutenbergSync.Core.Metadata;
using Serilog;

namespace GutenbergSync.Core.Catalog;

/// <summary>
/// Repository for storing and querying ebook metadata in SQLite using Entity Framework Core
/// </summary>
public sealed class CatalogRepository : ICatalogRepository
{
    private readonly string _connectionString;
    private readonly ILogger _logger;
    private readonly IServiceProvider _serviceProvider;

    public CatalogRepository(AppConfiguration config, ILogger logger, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        
        // FORCE correct path - never use config resolution
        const string correctPath = "/mnt/workspace/gutenberg/gutenberg.db";
        var dbPath = correctPath;
        
        // Log what we're using - EXTENSIVE logging
        _logger.Information("=== CatalogRepository constructor ===");
        _logger.Information("FORCED database path: {DbPath}", dbPath);
        _logger.Information("Config Catalog.DatabasePath: {ConfigDbPath}", config.Catalog.DatabasePath ?? "(null - using default)");
        _logger.Information("Config Sync.TargetDirectory: {TargetDir}", config.Sync.TargetDirectory);
        
        var fileExists = System.IO.File.Exists(dbPath);
        var fileSize = fileExists ? new System.IO.FileInfo(dbPath).Length : 0;
        _logger.Information("Database file exists: {Exists}, Size: {Size} bytes", fileExists, fileSize);
        
        if (!fileExists)
        {
            _logger.Error("CRITICAL: Database file does NOT exist at: {DbPath}", dbPath);
            throw new FileNotFoundException($"Database file does not exist at {dbPath}");
        }
        else if (fileSize == 0)
        {
            _logger.Error("CRITICAL: Database file exists but is EMPTY (0 bytes) at: {DbPath}", dbPath);
            throw new InvalidOperationException($"Database file is empty at {dbPath}");
        }
        
        // Use the FORCED path directly - it's already absolute
        // Don't use Path.GetFullPath as it may resolve /mnt paths incorrectly
        // Disable connection pooling for SQLite (causes corruption issues)
        // Use DELETE journal mode (not WAL) to avoid WAL file conflicts
        _connectionString = $"Data Source={dbPath};Cache=Shared;Pooling=False;Journal Mode=Delete;";
        
        _logger.Information("Connection string: {ConnectionString}", _connectionString);
        _logger.Information("Database path: {DbPath}", dbPath);
        _logger.Information("Path exists: {Exists}", System.IO.File.Exists(dbPath));
        _logger.Information("=== End CatalogRepository constructor ===");
    }

    /// <summary>
    /// Gets a DbContext instance from DI (preferred) or creates one if not available
    /// </summary>
    private CatalogDbContext GetDbContext()
    {
        // Try to get from DI first (web app)
        var context = _serviceProvider?.GetService<CatalogDbContext>();
        if (context != null)
        {
            return context;
        }
        
        // Fallback: create manually (CLI)
        return new CatalogDbContext(_connectionString, _logger);
    }
    
    /// <summary>
    /// Creates a new DbContext instance for database operations (async version for compatibility)
    /// </summary>
    private async Task<CatalogDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
    {
        // Try DI first (web app)
        var context = _serviceProvider?.GetService<CatalogDbContext>();
        if (context != null)
        {
            return context;
        }
        
        // Fallback: create manually (CLI)
        return new CatalogDbContext(_connectionString, _logger);
    }
    
    /// <summary>
    /// Creates a new DbContext instance (synchronous version for compatibility)
    /// </summary>
    private CatalogDbContext CreateDbContext()
    {
        return GetDbContext();
    }

    /// <summary>
    /// Resolves the database path from configuration using consistent logic:
    /// 1. Use Catalog.DatabasePath if explicitly set
    /// 2. Otherwise use Sync.TargetDirectory + "gutenberg.db"
    /// </summary>
    private static string ResolveDatabasePath(AppConfiguration config)
    {
        if (!string.IsNullOrWhiteSpace(config.Catalog.DatabasePath))
        {
            return config.Catalog.DatabasePath;
        }
        
        return Path.Combine(config.Sync.TargetDirectory, "gutenberg.db");
    }

    /// <inheritdoc/>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        // Extract database path from connection string
        var dbPath = _connectionString.Replace("Data Source=", "").Replace(";", "").Trim();
        
        _logger.Information("InitializeAsync called - Database path: {DbPath}", dbPath);
        
        // CRITICAL: Verify we're using the correct database path
        const string correctPath = "/mnt/workspace/gutenberg/gutenberg.db";
        if (dbPath != correctPath)
        {
            _logger.Error("CRITICAL: Attempting to initialize database at WRONG path: {DbPath}. Expected: {CorrectPath}", 
                dbPath, correctPath);
            throw new InvalidOperationException($"Cannot initialize database at {dbPath}. Must use {correctPath}");
        }
        
        // Verify the database file exists BEFORE opening connection
        // If it doesn't exist, don't create it - it should already exist
        if (!System.IO.File.Exists(dbPath))
        {
            _logger.Error("CRITICAL: Database file does not exist at: {DbPath}. Database must exist before initialization.", 
                dbPath);
            throw new FileNotFoundException($"Database file does not exist at {dbPath}. Please ensure the database is created first.");
        }
        
        // Verify it's not empty
        var fileInfo = new System.IO.FileInfo(dbPath);
        if (fileInfo.Length == 0)
        {
            _logger.Error("CRITICAL: Database file exists but is EMPTY (0 bytes) at: {DbPath}", dbPath);
            throw new InvalidOperationException($"Database file is empty at {dbPath}");
        }
        
        _logger.Information("Database file verified - exists: true, size: {Size} bytes", fileInfo.Length);
        
        // Use EF Core to ensure database is created and migrations are applied
        await using var context = await CreateDbContextAsync(cancellationToken);
        
        // Check if database can connect
        var canConnect = await context.Database.CanConnectAsync(cancellationToken);
        if (!canConnect)
        {
            _logger.Error("Cannot connect to database at: {DbPath}", dbPath);
            throw new InvalidOperationException($"Cannot connect to database at {dbPath}");
        }
        
        _logger.Information("Database connection verified");
        
        // Ensure database schema is up to date (this won't recreate existing tables)
        // For existing databases, we'll use raw SQL to add missing columns if needed
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        
        // Enable foreign keys
        var pragmaCommand = connection.CreateCommand();
        pragmaCommand.CommandText = "PRAGMA foreign_keys = ON;";
        await pragmaCommand.ExecuteNonQueryAsync(cancellationToken);
        
        // Add missing column if it doesn't exist (migration for existing databases)
        await AddMissingColumnsAsync(connection, cancellationToken);
        
        // Create indexes (IF NOT EXISTS) - EF Core will handle table creation, but we need indexes
        await CreateIndexesAsync(connection, cancellationToken);
        
        // Create FTS5 virtual table for full-text search (IF NOT EXISTS)
        await CreateFullTextSearchAsync(connection, cancellationToken);

        _logger.Information("Catalog database initialized");
    }

    private static async Task AddMissingColumnsAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        // Check if table exists first using raw SQL
        var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='ebooks'";
        var tableExists = await command.ExecuteScalarAsync(cancellationToken) as string;
        
        if (string.IsNullOrEmpty(tableExists))
        {
            // Table doesn't exist yet, it will be created with the column
            return;
        }
        
        // Check if column exists using pragma_table_info
        command.CommandText = "SELECT name, type FROM pragma_table_info('ebooks') WHERE name = 'local_file_size_bytes'";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var columnExists = await reader.ReadAsync(cancellationToken);
        await reader.CloseAsync();
        
        if (!columnExists)
        {
            // Column doesn't exist, add it
            try
            {
                command.CommandText = "ALTER TABLE ebooks ADD COLUMN local_file_size_bytes INTEGER;";
                await command.ExecuteNonQueryAsync(cancellationToken);
            }
            catch (Microsoft.Data.Sqlite.SqliteException ex) when (ex.Message.Contains("duplicate column") || ex.Message.Contains("already exists"))
            {
                // Column already exists, ignore
            }
            catch (Exception)
            {
                // Any other error - log but continue, column might be in wrong state
                // This is a migration, so we want to be lenient
            }
        }
    }

    private static async Task EnsureColumnExistsAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        // Try to add the column - SQLite will error if it already exists, which we ignore
        // This is simpler than checking PRAGMA table_info
        try
        {
            var command = connection.CreateCommand();
            command.CommandText = "ALTER TABLE ebooks ADD COLUMN local_file_size_bytes INTEGER;";
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (Microsoft.Data.Sqlite.SqliteException ex) when (ex.Message.Contains("duplicate column") || ex.Message.Contains("already exists"))
        {
            // Column already exists, that's fine
        }
        catch
        {
            // Table might not exist yet, that's also fine - it will be created with the column
        }
    }

    /// <inheritdoc/>
    public async Task UpsertAsync(EbookMetadata metadata, CancellationToken cancellationToken = default)
    {
        await using var context = await CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            // Upsert ebook
            await UpsertEbookAsync(context, metadata, cancellationToken);

            // Upsert authors
            await UpsertAuthorsAsync(context, metadata, cancellationToken);

            // Upsert subjects
            await UpsertSubjectsAsync(context, metadata, cancellationToken);

            // Upsert bookshelves
            await UpsertBookshelvesAsync(context, metadata, cancellationToken);

            await context.SaveChangesAsync(cancellationToken);
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
        await using var context = await CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            foreach (var metadata in items)
            {
                await UpsertEbookAsync(context, metadata, cancellationToken);
                await UpsertAuthorsAsync(context, metadata, cancellationToken);
                await UpsertSubjectsAsync(context, metadata, cancellationToken);
                await UpsertBookshelvesAsync(context, metadata, cancellationToken);
            }

            await context.SaveChangesAsync(cancellationToken);
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
        await using var context = await CreateDbContextAsync(cancellationToken);

        var ebook = await context.Ebooks
            .Include(e => e.EbookAuthors)
                .ThenInclude(ea => ea.Author)
            .Include(e => e.EbookSubjects)
            .Include(e => e.EbookBookshelves)
            .FirstOrDefaultAsync(e => e.BookId == bookId, cancellationToken);

        if (ebook == null)
            return null;

        return MapEntityToEbookMetadata(ebook);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<EbookMetadata>> SearchAsync(CatalogSearchOptions options, CancellationToken cancellationToken = default)
    {
        _logger.Information("SearchAsync: Query: {Query}, Author: {Author}, Language: {Language}", 
            options.Query ?? "(null)", options.Author ?? "(null)", options.Language ?? "(null)");

        await using var context = await CreateDbContextAsync(cancellationToken);
        
        // Build query
        var query = context.Ebooks.AsQueryable();
        
        if (!string.IsNullOrWhiteSpace(options.Query))
        {
            var queryTerm = options.Query.Trim();
            query = query.Where(e => e.Title != null && EF.Functions.Like(e.Title, $"%{queryTerm}%"));
        }
        
        if (!string.IsNullOrWhiteSpace(options.Author))
        {
            var authorTerm = options.Author.Trim();
            query = query.Where(e => e.EbookAuthors.Any(ea => EF.Functions.Like(ea.Author.Name, $"%{authorTerm}%")));
        }
        
        if (!string.IsNullOrWhiteSpace(options.Subject))
        {
            var subjectTerm = options.Subject.Trim();
            query = query.Where(e => e.EbookSubjects.Any(es => EF.Functions.Like(es.Subject, $"%{subjectTerm}%")));
        }
        
        if (!string.IsNullOrWhiteSpace(options.Language))
        {
            var languageTerm = options.Language.Trim();
            query = query.Where(e => 
                (e.Language != null && EF.Functions.Like(e.Language, $"%{languageTerm}%")) || 
                (e.LanguageIsoCode != null && e.LanguageIsoCode == languageTerm));
        }
        
        // Get basic entities first
        var ebooks = await query
            .OrderBy(e => e.BookId)
            .Skip(options.Offset)
            .Take(options.Limit ?? 50)
            .ToListAsync(cancellationToken);
        
        if (ebooks.Count == 0)
        {
            return Array.Empty<EbookMetadata>();
        }
        
        // Load related data
        foreach (var ebook in ebooks)
        {
            await context.Entry(ebook)
                .Collection(e => e.EbookAuthors)
                .Query()
                .Include(ea => ea.Author)
                .LoadAsync(cancellationToken);
            
            await context.Entry(ebook)
                .Collection(e => e.EbookSubjects)
                .LoadAsync(cancellationToken);
            
            await context.Entry(ebook)
                .Collection(e => e.EbookBookshelves)
                .LoadAsync(cancellationToken);
        }
        
        _logger.Information("SearchAsync: Found {Count} results", ebooks.Count);
        return ebooks.Select(MapEntityToEbookMetadata).ToList();
    }

    /// <inheritdoc/>
    public async Task<CatalogStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default)
    {
        _logger.Information("GetStatisticsAsync: Starting statistics query");
        
        // Use EXACT same pattern as before - create context manually, don't use DI
        await using var context = await CreateDbContextAsync(cancellationToken);
        
        // Check if ebooks table exists
        var canConnect = await context.Database.CanConnectAsync(cancellationToken);
        if (!canConnect)
        {
            _logger.Error("GetStatisticsAsync: Cannot connect to database");
            throw new InvalidOperationException("Cannot connect to database");
        }

        _logger.Information("GetStatisticsAsync: Database connection verified");

        // Use LINQ for all queries
        var totalBooks = await context.Ebooks.CountAsync(cancellationToken);
        _logger.Information("GetStatisticsAsync: totalBooks = {Count}", totalBooks);

        var totalAuthors = await context.Authors.CountAsync(cancellationToken);
        _logger.Information("GetStatisticsAsync: totalAuthors = {Count}", totalAuthors);

        var uniqueLanguages = await context.Ebooks
            .Where(e => e.Language != null || e.LanguageIsoCode != null)
            .Select(e => e.LanguageIsoCode ?? e.Language ?? "")
            .Distinct()
            .CountAsync(cancellationToken);
        _logger.Information("GetStatisticsAsync: uniqueLanguages = {Count}", uniqueLanguages);

        var uniqueSubjects = await context.EbookSubjects
            .Select(es => es.Subject)
            .Distinct()
            .CountAsync(cancellationToken);
        _logger.Information("GetStatisticsAsync: uniqueSubjects = {Count}", uniqueSubjects);

        // Total file size (if column exists)
        long totalFileSize = 0;
        try
        {
            totalFileSize = await context.Ebooks
                .Where(e => e.LocalFileSizeBytes.HasValue)
                .SumAsync(e => e.LocalFileSizeBytes ?? 0, cancellationToken);
            _logger.Information("GetStatisticsAsync: totalFileSize = {Size}", totalFileSize);
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "GetStatisticsAsync: Could not calculate file size, using 0");
            totalFileSize = 0;
        }

        // Date range
        var minDateStr = await context.Ebooks
            .Where(e => e.PublicationDate != null)
            .Select(e => e.PublicationDate!)
            .OrderBy(d => d)
            .FirstOrDefaultAsync(cancellationToken);

        var maxDateStr = await context.Ebooks
            .Where(e => e.PublicationDate != null)
            .Select(e => e.PublicationDate!)
            .OrderByDescending(d => d)
            .FirstOrDefaultAsync(cancellationToken);

        DateRange? publicationDateRange = null;
        if (!string.IsNullOrWhiteSpace(minDateStr) || !string.IsNullOrWhiteSpace(maxDateStr))
        {
            DateOnly? minDate = null;
            DateOnly? maxDate = null;
            
            if (!string.IsNullOrWhiteSpace(minDateStr) && DateOnly.TryParse(minDateStr, out var min))
                minDate = min;
            
            if (!string.IsNullOrWhiteSpace(maxDateStr) && DateOnly.TryParse(maxDateStr, out var max))
                maxDate = max;
            
            if (minDate.HasValue || maxDate.HasValue)
            {
                publicationDateRange = new DateRange { Start = minDate, End = maxDate };
            }
        }

        // ID range
        var minId = await context.Ebooks.MinAsync(e => (int?)e.BookId, cancellationToken);
        var maxId = await context.Ebooks.MaxAsync(e => (int?)e.BookId, cancellationToken);

        var result = new CatalogStatistics
        {
            TotalBooks = totalBooks,
            TotalAuthors = totalAuthors,
            UniqueLanguages = uniqueLanguages,
            UniqueSubjects = uniqueSubjects,
            TotalFileSizeBytes = totalFileSize,
            PublicationDateRange = publicationDateRange,
            BookIdRange = minId.HasValue || maxId.HasValue
                ? new IdRange { Start = minId, End = maxId }
                : null
        };
        
        _logger.Information("GetStatisticsAsync: Returning result with {Books} books", result.TotalBooks);
        return result;
    }

    /// <inheritdoc/>
    public async Task ExportToCsvAsync(string outputPath, CancellationToken cancellationToken = default)
    {
        await using var context = await CreateDbContextAsync(cancellationToken);

        var ebooks = await context.Ebooks
            .Include(e => e.EbookAuthors)
                .ThenInclude(ea => ea.Author)
            .Include(e => e.EbookSubjects)
            .OrderBy(e => e.BookId)
            .ToListAsync(cancellationToken);

        await using var writer = new StreamWriter(outputPath, false, Encoding.UTF8);
        
        // Write header
        await writer.WriteLineAsync("BookId,Title,Language,LanguageIsoCode,PublicationDate,Subjects,Authors,Rights,DownloadCount");

        foreach (var ebook in ebooks)
        {
            var authors = ebook.EbookAuthors.Select(ea => ea.Author.Name).ToList();
            var subjects = ebook.EbookSubjects.Select(es => es.Subject).ToList();

            var publicationDateStr = ebook.PublicationDate != null && DateOnly.TryParse(ebook.PublicationDate, out var pubDate) 
                ? pubDate.ToString("yyyy-MM-dd") 
                : "";
            var line = $"{ebook.BookId},\"{EscapeCsv(ebook.Title)}\",\"{EscapeCsv(ebook.Language ?? "")}\",\"{EscapeCsv(ebook.LanguageIsoCode ?? "")}\",\"{publicationDateStr}\",\"{EscapeCsv(string.Join("; ", subjects))}\",\"{EscapeCsv(string.Join("; ", authors))}\",\"{EscapeCsv(ebook.Rights ?? "")}\",{ebook.DownloadCount ?? 0}";
            await writer.WriteLineAsync(line);
        }

        _logger.Information("Exported catalog to CSV: {Path}", outputPath);
    }

    /// <inheritdoc/>
    public async Task ExportToJsonAsync(string outputPath, CancellationToken cancellationToken = default)
    {
        var exportData = new List<object>();

        // Use GetByIdAsync which already uses EF Core
        await using var context = await CreateDbContextAsync(cancellationToken);
        var bookIds = await context.Ebooks
            .OrderBy(e => e.BookId)
            .Select(e => e.BookId)
            .ToListAsync(cancellationToken);

        foreach (var bookId in bookIds)
        {
            var metadata = await GetByIdAsync(bookId, cancellationToken);
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
                local_file_size_bytes INTEGER,
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

        var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task CreateIndexesAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        var sql = @"
            CREATE INDEX IF NOT EXISTS idx_ebooks_language ON ebooks(language);
            CREATE INDEX IF NOT EXISTS idx_ebooks_language_iso ON ebooks(language_iso_code);
            CREATE INDEX IF NOT EXISTS idx_ebooks_publication_date ON ebooks(publication_date);
            CREATE INDEX IF NOT EXISTS idx_authors_name ON authors(name);
        ";

        var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken);
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

        var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task UpsertEbookAsync(CatalogDbContext context, EbookMetadata metadata, CancellationToken cancellationToken)
    {
        var ebook = await context.Ebooks.FindAsync(new object[] { metadata.BookId }, cancellationToken);
        
        if (ebook == null)
        {
            ebook = new EbookEntity
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
                Checksum = metadata.Checksum,
                LocalFileSizeBytes = metadata.LocalFileSizeBytes,
                CreatedUtc = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"),
                UpdatedUtc = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")
            };
            context.Ebooks.Add(ebook);
        }
        else
        {
            ebook.Title = metadata.Title;
            ebook.Language = metadata.Language;
            ebook.LanguageIsoCode = metadata.LanguageIsoCode;
            ebook.PublicationDate = metadata.PublicationDate?.ToString("yyyy-MM-dd");
            ebook.Rights = metadata.Rights;
            ebook.DownloadCount = metadata.DownloadCount;
            ebook.RdfPath = metadata.RdfPath;
            ebook.VerifiedUtc = metadata.VerifiedUtc?.ToString("yyyy-MM-dd HH:mm:ss");
            ebook.Checksum = metadata.Checksum;
            ebook.LocalFileSizeBytes = metadata.LocalFileSizeBytes;
            ebook.UpdatedUtc = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
            context.Ebooks.Update(ebook);
        }
    }

    private static async Task UpsertAuthorsAsync(CatalogDbContext context, EbookMetadata metadata, CancellationToken cancellationToken)
    {
        // Get the ebook entity
        var ebook = await context.Ebooks
            .Include(e => e.EbookAuthors)
            .FirstOrDefaultAsync(e => e.BookId == metadata.BookId, cancellationToken);
        
        if (ebook == null) return;

        // Remove existing associations
        context.EbookAuthors.RemoveRange(ebook.EbookAuthors);

        foreach (var author in metadata.Authors)
        {
            // Find or create author
            var authorEntity = await context.Authors
                .FirstOrDefaultAsync(a => a.Name == author.Name, cancellationToken);

            if (authorEntity == null)
            {
                authorEntity = new AuthorEntity { Name = author.Name };
                context.Authors.Add(authorEntity);
                await context.SaveChangesAsync(cancellationToken); // Save to get the ID
            }

            // Associate with ebook
            var ebookAuthor = new EbookAuthor
            {
                EbookId = metadata.BookId,
                AuthorId = authorEntity.Id
            };
            context.EbookAuthors.Add(ebookAuthor);
        }
    }

    private static async Task UpsertSubjectsAsync(CatalogDbContext context, EbookMetadata metadata, CancellationToken cancellationToken)
    {
        // Get the ebook entity
        var ebook = await context.Ebooks
            .Include(e => e.EbookSubjects)
            .FirstOrDefaultAsync(e => e.BookId == metadata.BookId, cancellationToken);
        
        if (ebook == null) return;

        // Remove existing subjects
        context.EbookSubjects.RemoveRange(ebook.EbookSubjects);

        // Add new subjects
        foreach (var subject in metadata.Subjects)
        {
            var ebookSubject = new EbookSubject
            {
                EbookId = metadata.BookId,
                Subject = subject
            };
            context.EbookSubjects.Add(ebookSubject);
        }
    }

    private static async Task UpsertBookshelvesAsync(CatalogDbContext context, EbookMetadata metadata, CancellationToken cancellationToken)
    {
        // Get the ebook entity
        var ebook = await context.Ebooks
            .Include(e => e.EbookBookshelves)
            .FirstOrDefaultAsync(e => e.BookId == metadata.BookId, cancellationToken);
        
        if (ebook == null) return;

        // Remove existing bookshelves
        context.EbookBookshelves.RemoveRange(ebook.EbookBookshelves);

        // Add new bookshelves
        foreach (var bookshelf in metadata.Bookshelves)
        {
            var ebookBookshelf = new EbookBookshelf
            {
                EbookId = metadata.BookId,
                Bookshelf = bookshelf
            };
            context.EbookBookshelves.Add(ebookBookshelf);
        }
    }


    /// <summary>
    /// Maps an EF Core EbookEntity to EbookMetadata
    /// </summary>
    private static EbookMetadata MapEntityToEbookMetadata(EbookEntity ebook)
    {
        return new EbookMetadata
        {
            BookId = ebook.BookId,
            Title = ebook.Title,
            Authors = ebook.EbookAuthors.Select(ea => new Author
            {
                Name = ea.Author.Name,
                BirthYear = null, // AuthorEntity doesn't have these fields - would need to add if needed
                DeathYear = null,
                WebPage = null
            }).ToList(),
            Language = ebook.Language,
            LanguageIsoCode = ebook.LanguageIsoCode,
            PublicationDate = ebook.PublicationDate != null && DateOnly.TryParse(ebook.PublicationDate, out var date) ? date : null,
            Subjects = ebook.EbookSubjects.Select(es => es.Subject).ToList(),
            Bookshelves = ebook.EbookBookshelves.Select(eb => eb.Bookshelf).ToList(),
            Rights = ebook.Rights,
            DownloadCount = ebook.DownloadCount,
            RdfPath = ebook.RdfPath,
            VerifiedUtc = ebook.VerifiedUtc != null && DateTime.TryParse(ebook.VerifiedUtc, null, System.Globalization.DateTimeStyles.None, out var verified) ? verified : null,
            Checksum = ebook.Checksum,
            LocalFileSizeBytes = ebook.LocalFileSizeBytes
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

}


