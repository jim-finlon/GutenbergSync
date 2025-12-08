namespace GutenbergSync.Core.Metadata;

/// <summary>
/// Represents an author of a Project Gutenberg ebook
/// </summary>
public sealed record Author
{
    /// <summary>
    /// Author's name
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Author's birth year (if known)
    /// </summary>
    public int? BirthYear { get; init; }

    /// <summary>
    /// Author's death year (if known)
    /// </summary>
    public int? DeathYear { get; init; }

    /// <summary>
    /// Author's web page URL (if available)
    /// </summary>
    public string? WebPage { get; init; }
}

