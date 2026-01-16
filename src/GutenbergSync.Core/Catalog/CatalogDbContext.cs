using Microsoft.EntityFrameworkCore;
using GutenbergSync.Core.Configuration;

namespace GutenbergSync.Core.Catalog;

/// <summary>
/// Entity Framework Core DbContext for the catalog database
/// </summary>
public class CatalogDbContext : DbContext
{
    private readonly string? _connectionString;
    private readonly Serilog.ILogger? _logger;

    // Constructor for DI (web app)
    public CatalogDbContext(DbContextOptions<CatalogDbContext> options) : base(options)
    {
    }

    // Constructor for manual creation (CLI)
    public CatalogDbContext(string connectionString, Serilog.ILogger logger)
    {
        _connectionString = connectionString;
        _logger = logger;
    }

    public DbSet<EbookEntity> Ebooks { get; set; } = null!;
    public DbSet<AuthorEntity> Authors { get; set; } = null!;
    public DbSet<EbookAuthor> EbookAuthors { get; set; } = null!;
    public DbSet<EbookSubject> EbookSubjects { get; set; } = null!;
    public DbSet<EbookBookshelf> EbookBookshelves { get; set; } = null!;

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        // Only configure if not already configured (i.e., when created manually, not via DI)
        if (!optionsBuilder.IsConfigured && _connectionString != null)
        {
            optionsBuilder.UseSqlite(_connectionString, options =>
            {
                options.CommandTimeout(30);
            });
            optionsBuilder.EnableSensitiveDataLogging(false);
            optionsBuilder.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Ebook entity
        modelBuilder.Entity<EbookEntity>(entity =>
        {
            entity.ToTable("ebooks");
            entity.HasKey(e => e.BookId);
            entity.Property(e => e.BookId).HasColumnName("book_id");
            entity.Property(e => e.Title).HasColumnName("title").IsRequired();
            entity.Property(e => e.Language).HasColumnName("language");
            entity.Property(e => e.LanguageIsoCode).HasColumnName("language_iso_code");
            entity.Property(e => e.PublicationDate).HasColumnName("publication_date");
            entity.Property(e => e.Rights).HasColumnName("rights");
            entity.Property(e => e.DownloadCount).HasColumnName("download_count");
            entity.Property(e => e.RdfPath).HasColumnName("rdf_path");
            entity.Property(e => e.VerifiedUtc).HasColumnName("verified_utc");
            entity.Property(e => e.Checksum).HasColumnName("checksum");
            entity.Property(e => e.LocalFileSizeBytes).HasColumnName("local_file_size_bytes");
            entity.Property(e => e.CreatedUtc).HasColumnName("created_utc").HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.UpdatedUtc).HasColumnName("updated_utc").HasDefaultValueSql("CURRENT_TIMESTAMP");

            // Relationships
            entity.HasMany(e => e.EbookAuthors)
                .WithOne(ea => ea.Ebook)
                .HasForeignKey(ea => ea.EbookId);

            entity.HasMany(e => e.EbookSubjects)
                .WithOne(es => es.Ebook)
                .HasForeignKey(es => es.EbookId);

            entity.HasMany(e => e.EbookBookshelves)
                .WithOne(eb => eb.Ebook)
                .HasForeignKey(eb => eb.EbookId);
        });

        // Author entity
        modelBuilder.Entity<AuthorEntity>(entity =>
        {
            entity.ToTable("authors");
            entity.HasKey(a => a.Id);
            entity.Property(a => a.Id).HasColumnName("id");
            entity.Property(a => a.Name).HasColumnName("name").IsRequired();
            entity.HasIndex(a => a.Name);

            entity.HasMany(a => a.EbookAuthors)
                .WithOne(ea => ea.Author)
                .HasForeignKey(ea => ea.AuthorId);
        });

        // EbookAuthor junction table
        modelBuilder.Entity<EbookAuthor>(entity =>
        {
            entity.ToTable("ebook_authors");
            entity.HasKey(ea => new { ea.EbookId, ea.AuthorId });
            entity.Property(ea => ea.EbookId).HasColumnName("ebook_id");
            entity.Property(ea => ea.AuthorId).HasColumnName("author_id");
        });

        // EbookSubject junction table
        modelBuilder.Entity<EbookSubject>(entity =>
        {
            entity.ToTable("ebook_subjects");
            entity.HasKey(es => new { es.EbookId, es.Subject });
            entity.Property(es => es.EbookId).HasColumnName("ebook_id");
            entity.Property(es => es.Subject).HasColumnName("subject").IsRequired();
            entity.HasIndex(es => es.Subject);
        });

        // EbookBookshelf junction table
        modelBuilder.Entity<EbookBookshelf>(entity =>
        {
            entity.ToTable("ebook_bookshelves");
            entity.HasKey(eb => new { eb.EbookId, eb.Bookshelf });
            entity.Property(eb => eb.EbookId).HasColumnName("ebook_id");
            entity.Property(eb => eb.Bookshelf).HasColumnName("bookshelf").IsRequired();
            entity.HasIndex(eb => eb.Bookshelf);
        });

        // Create FTS5 virtual table (still needed for full-text search if we want to use it later)
        // For now, we'll use LINQ Contains/StartsWith for search
    }
}

