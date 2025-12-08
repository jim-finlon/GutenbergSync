using System.Xml.Linq;
using Serilog;

namespace GutenbergSync.Core.Metadata;

/// <summary>
/// Parser for Project Gutenberg RDF/XML metadata files
/// </summary>
public sealed class RdfParser : IRdfParser
{
    private readonly ILanguageMapper _languageMapper;
    private readonly ILogger _logger;

    public RdfParser(ILanguageMapper languageMapper, ILogger logger)
    {
        _languageMapper = languageMapper;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<EbookMetadata> ParseFileAsync(string rdfFilePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(rdfFilePath))
        {
            throw new FileNotFoundException($"RDF file not found: {rdfFilePath}");
        }

        await using var stream = File.OpenRead(rdfFilePath);
        return await ParseXmlAsync(stream, cancellationToken);
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<EbookMetadata> ParseDirectoryAsync(
        string directoryPath,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(directoryPath))
        {
            yield break;
        }

        var rdfFiles = Directory.EnumerateFiles(directoryPath, "*.rdf", SearchOption.AllDirectories);

        foreach (var rdfFile in rdfFiles)
        {
            if (cancellationToken.IsCancellationRequested)
                yield break;

            EbookMetadata? metadata = null;
            try
            {
                metadata = await ParseFileAsync(rdfFile, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, "Failed to parse RDF file: {RdfFile}", rdfFile);
                // Continue with next file
            }

            if (metadata != null)
            {
                yield return metadata;
            }
        }
    }

    /// <inheritdoc/>
    public async Task<EbookMetadata> ParseXmlAsync(Stream rdfStream, CancellationToken cancellationToken = default)
    {
        var doc = await XDocument.LoadAsync(rdfStream, LoadOptions.None, cancellationToken);
        return ParseDocument(doc);
    }

    private EbookMetadata ParseDocument(XDocument doc)
    {
        var rdf = XNamespace.Get(RdfNamespaces.Rdf);
        var dcTerms = XNamespace.Get(RdfNamespaces.DcTerms);
        var pgTerms = XNamespace.Get(RdfNamespaces.PgTerms);

        // Find the ebook description (rdf:Description with pgterms:ebook)
        var ebookElement = doc.Descendants(rdf + "Description")
            .FirstOrDefault(e => e.Element(pgTerms + "ebook") != null);

        if (ebookElement == null)
        {
            throw new InvalidOperationException("No ebook description found in RDF document");
        }

        // Extract book ID from about attribute or pgterms:ebook
        var bookId = ExtractBookId(ebookElement, rdf, pgTerms);

        // Extract title
        var title = ebookElement.Element(dcTerms + "title")?.Value ?? "Unknown";

        // Extract authors
        var authors = ExtractAuthors(ebookElement, pgTerms, dcTerms);

        // Extract language
        var (language, languageIsoCode) = ExtractLanguage(ebookElement, dcTerms);

        // Extract publication date
        var publicationDate = ExtractPublicationDate(ebookElement, dcTerms);

        // Extract subjects
        var subjects = ebookElement.Elements(dcTerms + "subject")
            .Select(e => e.Element(rdf + "Description")?.Element(dcTerms + "value")?.Value)
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Cast<string>()
            .ToList();

        // Extract bookshelves
        var bookshelves = ebookElement.Elements(pgTerms + "bookshelf")
            .Select(e => e.Element(dcTerms + "value")?.Value)
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Cast<string>()
            .ToList();

        // Extract rights
        var rights = ebookElement.Element(dcTerms + "rights")?.Value;

        // Extract download count
        var downloadCount = ExtractDownloadCount(ebookElement, pgTerms);

        // Extract RDF path (if available in metadata)
        var rdfPath = ebookElement.Attribute(rdf + "about")?.Value;

        return new EbookMetadata
        {
            BookId = bookId,
            Title = title,
            Authors = authors,
            Language = language,
            LanguageIsoCode = languageIsoCode,
            PublicationDate = publicationDate,
            Subjects = subjects,
            Bookshelves = bookshelves,
            Rights = rights,
            DownloadCount = downloadCount,
            RdfPath = rdfPath
        };
    }

    private static int ExtractBookId(XElement ebookElement, XNamespace rdf, XNamespace pgTerms)
    {
        // Try to get from pgterms:ebook rdf:about
        var ebookRef = ebookElement.Element(pgTerms + "ebook");
        if (ebookRef != null)
        {
            var about = ebookRef.Attribute(rdf + "resource")?.Value;
            if (about != null)
            {
                // Extract ID from URL like "http://www.gutenberg.org/ebooks/12345"
                var match = System.Text.RegularExpressions.Regex.Match(about, @"/(\d+)(?:\.rdf)?$");
                if (match.Success && int.TryParse(match.Groups[1].Value, out var id))
                {
                    return id;
                }
            }
        }

        // Try to get from rdf:about on the description element
        var aboutAttr = ebookElement.Attribute(rdf + "about")?.Value;
        if (aboutAttr != null)
        {
            var match = System.Text.RegularExpressions.Regex.Match(aboutAttr, @"/(\d+)(?:\.rdf)?$");
            if (match.Success && int.TryParse(match.Groups[1].Value, out var id))
            {
                return id;
            }
        }

        throw new InvalidOperationException("Could not extract book ID from RDF");
    }

    private List<Author> ExtractAuthors(XElement ebookElement, XNamespace pgTerms, XNamespace dcTerms)
    {
        var authors = new List<Author>();

        // Project Gutenberg uses pgterms:agent for authors
        var agentElements = ebookElement.Elements(pgTerms + "agent");

        foreach (var agentElement in agentElements)
        {
            var name = agentElement.Element(pgTerms + "name")?.Value;
            if (string.IsNullOrWhiteSpace(name))
            {
                // Fallback to dc:creator
                name = agentElement.Element(dcTerms + "creator")?.Value;
            }

            if (string.IsNullOrWhiteSpace(name))
                continue;

            // Extract birth/death years
            var birthYear = ExtractYear(agentElement, pgTerms + "birthdate");
            var deathYear = ExtractYear(agentElement, pgTerms + "deathdate");

            // Extract web page
            var webPage = agentElement.Element(pgTerms + "webpage")?.Attribute(XNamespace.Get(RdfNamespaces.Rdf) + "resource")?.Value;

            authors.Add(new Author
            {
                Name = name,
                BirthYear = birthYear,
                DeathYear = deathYear,
                WebPage = webPage
            });
        }

        // Fallback to dc:creator if no agents found
        if (authors.Count == 0)
        {
            var creators = ebookElement.Elements(dcTerms + "creator")
                .Select(e => e.Value)
                .Where(v => !string.IsNullOrWhiteSpace(v));

            authors.AddRange(creators.Select(name => new Author { Name = name }));
        }

        return authors;
    }

    private static int? ExtractYear(XElement element, XName dateElementName)
    {
        var dateElement = element.Element(dateElementName);
        if (dateElement == null)
            return null;

        var dateValue = dateElement.Value;
        if (string.IsNullOrWhiteSpace(dateValue))
            return null;

        // Try to extract year from date string (e.g., "1865-01-01" or "1865")
        var yearMatch = System.Text.RegularExpressions.Regex.Match(dateValue, @"^(\d{4})");
        if (yearMatch.Success && int.TryParse(yearMatch.Groups[1].Value, out var year))
        {
            return year;
        }

        return null;
    }

    private (string? Language, string? LanguageIsoCode) ExtractLanguage(XElement ebookElement, XNamespace dcTerms)
    {
        var languageElement = ebookElement.Element(dcTerms + "language");
        if (languageElement == null)
            return (null, null);

        var languageValue = languageElement.Element(XNamespace.Get(RdfNamespaces.Rdf) + "Description")
            ?.Element(dcTerms + "value")
            ?.Value;

        if (string.IsNullOrWhiteSpace(languageValue))
            return (null, null);

        // Try to map using LanguageMapper
        var mapped = _languageMapper.TryMap(languageValue, out var isoCode, out var languageName);

        if (mapped)
        {
            return (languageName ?? languageValue, isoCode);
        }

        // If mapping failed, check if it's already an ISO code
        if (languageValue.Length == 2 || languageValue.Length == 3)
        {
            return (null, languageValue.ToLowerInvariant());
        }

        return (languageValue, null);
    }

    private static DateOnly? ExtractPublicationDate(XElement ebookElement, XNamespace dcTerms)
    {
        var issuedElement = ebookElement.Element(dcTerms + "issued");
        if (issuedElement == null)
            return null;

        var dateValue = issuedElement.Value;
        if (string.IsNullOrWhiteSpace(dateValue))
            return null;

        // Try to parse date (format: YYYY-MM-DD or YYYY)
        if (DateOnly.TryParse(dateValue, out var date))
        {
            return date;
        }

        // Try to extract year and create date
        var yearMatch = System.Text.RegularExpressions.Regex.Match(dateValue, @"^(\d{4})");
        if (yearMatch.Success && int.TryParse(yearMatch.Groups[1].Value, out var year))
        {
            return new DateOnly(year, 1, 1);
        }

        return null;
    }

    private static int? ExtractDownloadCount(XElement ebookElement, XNamespace pgTerms)
    {
        var downloadsElement = ebookElement.Element(pgTerms + "downloads");
        if (downloadsElement == null)
            return null;

        if (int.TryParse(downloadsElement.Value, out var count))
        {
            return count;
        }

        return null;
    }
}

