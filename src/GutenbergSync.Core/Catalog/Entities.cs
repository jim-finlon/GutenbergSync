namespace GutenbergSync.Core.Catalog;

/// <summary>
/// Ebook entity for EF Core
/// </summary>
public class EbookEntity
{
    public int BookId { get; set; }
    public string Title { get; set; } = "";
    public string? Language { get; set; }
    public string? LanguageIsoCode { get; set; }
    public string? PublicationDate { get; set; }
    public string? Rights { get; set; }
    public int? DownloadCount { get; set; }
    public string? RdfPath { get; set; }
    public string? VerifiedUtc { get; set; }
    public string? Checksum { get; set; }
    public long? LocalFileSizeBytes { get; set; }
    public string? CreatedUtc { get; set; }
    public string? UpdatedUtc { get; set; }

    // Navigation properties
    public ICollection<EbookAuthor> EbookAuthors { get; set; } = new List<EbookAuthor>();
    public ICollection<EbookSubject> EbookSubjects { get; set; } = new List<EbookSubject>();
    public ICollection<EbookBookshelf> EbookBookshelves { get; set; } = new List<EbookBookshelf>();
}

/// <summary>
/// Author entity for EF Core
/// </summary>
public class AuthorEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = "";

    // Navigation properties
    public ICollection<EbookAuthor> EbookAuthors { get; set; } = new List<EbookAuthor>();
}

/// <summary>
/// Junction table for ebook-author many-to-many relationship
/// </summary>
public class EbookAuthor
{
    public int EbookId { get; set; }
    public int AuthorId { get; set; }

    // Navigation properties
    public EbookEntity Ebook { get; set; } = null!;
    public AuthorEntity Author { get; set; } = null!;
}

/// <summary>
/// Junction table for ebook-subject many-to-many relationship
/// </summary>
public class EbookSubject
{
    public int EbookId { get; set; }
    public string Subject { get; set; } = "";

    // Navigation properties
    public EbookEntity Ebook { get; set; } = null!;
}

/// <summary>
/// Junction table for ebook-bookshelf many-to-many relationship
/// </summary>
public class EbookBookshelf
{
    public int EbookId { get; set; }
    public string Bookshelf { get; set; } = "";

    // Navigation properties
    public EbookEntity Ebook { get; set; } = null!;
}

