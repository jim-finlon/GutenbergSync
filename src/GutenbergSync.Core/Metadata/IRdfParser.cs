namespace GutenbergSync.Core.Metadata;

/// <summary>
/// Parser for Project Gutenberg RDF/XML metadata files
/// </summary>
public interface IRdfParser
{
    /// <summary>
    /// Parses an RDF file and returns ebook metadata
    /// </summary>
    Task<EbookMetadata> ParseFileAsync(string rdfFilePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Parses all RDF files in a directory
    /// </summary>
    IAsyncEnumerable<EbookMetadata> ParseDirectoryAsync(
        string directoryPath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Parses RDF XML from a stream
    /// </summary>
    Task<EbookMetadata> ParseXmlAsync(Stream rdfStream, CancellationToken cancellationToken = default);
}

