using GutenbergSync.Core.Configuration;
using GutenbergSync.Core.Catalog;

namespace GutenbergSync.Web.Services;

public class EpubCopyService : IEpubCopyService
{
    private readonly AppConfiguration _config;
    private readonly ICatalogRepository _catalog;
    private readonly ILogger<EpubCopyService> _logger;

    public EpubCopyService(AppConfiguration config, ICatalogRepository catalog, ILogger<EpubCopyService> logger)
    {
        _config = config;
        _catalog = catalog;
        _logger = logger;
    }

    public async Task<string?> FindEpubPathAsync(int bookId)
    {
        var epubDir = Path.Combine(_config.Sync.TargetDirectory, "gutenberg-epub", bookId.ToString());
        
        if (!Directory.Exists(epubDir))
            return null;

        var epubFiles = Directory.GetFiles(epubDir, "*.epub");
        return epubFiles.FirstOrDefault();
    }

    public async Task<bool> CopyEpubAsync(int bookId, string destinationPath)
    {
        var sourcePath = await FindEpubPathAsync(bookId);
        if (sourcePath == null)
            return false;

        try
        {
            var destDir = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
            {
                Directory.CreateDirectory(destDir);
            }

            File.Copy(sourcePath, destinationPath, overwrite: true);
            _logger.LogInformation("Copied EPUB {BookId} from {Source} to {Dest}", bookId, sourcePath, destinationPath);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to copy EPUB {BookId}", bookId);
            return false;
        }
    }

    public async Task<string?> GenerateFilenameAsync(int bookId)
    {
        var book = await _catalog.GetByIdAsync(bookId);
        if (book == null)
            return null;

        // Generate filename: {author name}-{Book Title}.epub
        var authorName = book.Authors.Count > 0 
            ? SanitizeFilename(book.Authors[0].Name)
            : "Unknown";
        var title = SanitizeFilename(book.Title);
        return $"{authorName}-{title}.epub";
    }

    private static string SanitizeFilename(string filename)
    {
        // Remove invalid filename characters
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = string.Join("_", filename.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries));
        // Also remove other problematic characters
        sanitized = sanitized.Replace("<", "_").Replace(">", "_")
                             .Replace(":", "_").Replace("\"", "_")
                             .Replace("/", "_").Replace("\\", "_")
                             .Replace("|", "_").Replace("?", "_")
                             .Replace("*", "_");
        return sanitized.Trim();
    }
}

