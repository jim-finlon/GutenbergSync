using System.Security.Cryptography;
using System.Text;
using GutenbergSync.Core.Catalog;
using GutenbergSync.Core.Metadata;
using Serilog;

namespace GutenbergSync.Core.Sync;

/// <summary>
/// Service for auditing and verifying file integrity
/// </summary>
public sealed class AuditService : IAuditService
{
    private readonly ICatalogRepository _catalogRepository;
    private readonly ILogger _logger;

    public AuditService(ICatalogRepository catalogRepository, ILogger logger)
    {
        _catalogRepository = catalogRepository;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<FileVerificationResult> VerifyFileAsync(
        string filePath,
        long? expectedSize = null,
        string? expectedChecksum = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                return new FileVerificationResult
                {
                    FilePath = filePath,
                    Exists = false,
                    IsValid = false,
                    Status = FileVerificationStatus.Missing,
                    ExpectedSize = expectedSize,
                    ExpectedChecksum = expectedChecksum
                };
            }

            var fileInfo = new FileInfo(filePath);
            var actualSize = fileInfo.Length;

            // Check size if expected size is provided
            if (expectedSize.HasValue && actualSize != expectedSize.Value)
            {
                return new FileVerificationResult
                {
                    FilePath = filePath,
                    Exists = true,
                    IsValid = false,
                    ActualSize = actualSize,
                    ExpectedSize = expectedSize,
                    Status = FileVerificationStatus.SizeMismatch,
                    ErrorMessage = $"Size mismatch: expected {expectedSize}, actual {actualSize}"
                };
            }

            // Calculate checksum if expected checksum is provided
            string? actualChecksum = null;
            if (!string.IsNullOrWhiteSpace(expectedChecksum))
            {
                actualChecksum = await CalculateChecksumAsync(filePath, cancellationToken);

                if (!string.Equals(actualChecksum, expectedChecksum, StringComparison.OrdinalIgnoreCase))
                {
                    return new FileVerificationResult
                    {
                        FilePath = filePath,
                        Exists = true,
                        IsValid = false,
                        ActualSize = actualSize,
                        ExpectedSize = expectedSize,
                        ActualChecksum = actualChecksum,
                        ExpectedChecksum = expectedChecksum,
                        Status = FileVerificationStatus.ChecksumMismatch,
                        ErrorMessage = "Checksum mismatch"
                    };
                }
            }

            // Try to read file to check if it's corrupt
            try
            {
                await using var stream = File.OpenRead(filePath);
                var buffer = new byte[1024];
                var bytesRead = await stream.ReadAsync(buffer, cancellationToken);
                // Verify we can read at least some data (or EOF is fine)
                _ = bytesRead; // Acknowledge bytes read
            }
            catch (Exception ex)
            {
                return new FileVerificationResult
                {
                    FilePath = filePath,
                    Exists = true,
                    IsValid = false,
                    ActualSize = actualSize,
                    ExpectedSize = expectedSize,
                    ActualChecksum = actualChecksum,
                    ExpectedChecksum = expectedChecksum,
                    Status = FileVerificationStatus.Corrupt,
                    ErrorMessage = $"File is corrupt: {ex.Message}"
                };
            }

            return new FileVerificationResult
            {
                FilePath = filePath,
                Exists = true,
                IsValid = true,
                ActualSize = actualSize,
                ExpectedSize = expectedSize,
                ActualChecksum = actualChecksum,
                ExpectedChecksum = expectedChecksum,
                Status = FileVerificationStatus.Valid
            };
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error verifying file {FilePath}", filePath);
            return new FileVerificationResult
            {
                FilePath = filePath,
                Exists = false,
                IsValid = false,
                Status = FileVerificationStatus.Error,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <inheritdoc/>
    public async Task<AuditScanResult> ScanDirectoryAsync(
        string directoryPath,
        IProgress<AuditProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        var results = new List<FileVerificationResult>();

        if (!Directory.Exists(directoryPath))
        {
            return new AuditScanResult
            {
                TotalFiles = 0,
                Duration = DateTime.UtcNow - startTime
            };
        }

        var files = Directory.GetFiles(directoryPath, "*", SearchOption.AllDirectories).ToList();
        var totalFiles = files.Count;
        var processed = 0;

        foreach (var file in files)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            progress?.Report(new AuditProgress
            {
                CurrentFile = file,
                FilesProcessed = processed,
                TotalFiles = totalFiles
            });

            var result = await VerifyFileAsync(file, cancellationToken: cancellationToken);
            results.Add(result);

            processed++;
        }

        progress?.Report(new AuditProgress
        {
            CurrentFile = null,
            FilesProcessed = processed,
            TotalFiles = totalFiles
        });

        return new AuditScanResult
        {
            TotalFiles = totalFiles,
            ValidFiles = results.Count(r => r.Status == FileVerificationStatus.Valid),
            MissingFiles = results.Count(r => r.Status == FileVerificationStatus.Missing),
            CorruptFiles = results.Count(r => r.Status == FileVerificationStatus.Corrupt),
            SizeMismatchFiles = results.Count(r => r.Status == FileVerificationStatus.SizeMismatch),
            ChecksumMismatchFiles = results.Count(r => r.Status == FileVerificationStatus.ChecksumMismatch),
            Results = results,
            Duration = DateTime.UtcNow - startTime
        };
    }

    /// <inheritdoc/>
    public async Task<CatalogVerificationResult> VerifyCatalogAsync(
        IProgress<AuditProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        var results = new List<FileVerificationResult>();

        // Get all books from catalog
        var books = await _catalogRepository.SearchAsync(
            new CatalogSearchOptions { Limit = null },
            cancellationToken);

        var totalBooks = books.Count;
        var processed = 0;

        foreach (var book in books)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            progress?.Report(new AuditProgress
            {
                CurrentFile = $"Book {book.BookId}: {book.Title}",
                FilesProcessed = processed,
                TotalFiles = totalBooks
            });

            // Verify primary text file if available
            // Note: This is a simplified check - in reality, we'd check all format URLs
            var textFile = FindTextFile(book);
            if (textFile != null)
            {
                var result = await VerifyFileAsync(
                    textFile,
                    expectedSize: null, // Size not always available in metadata
                    expectedChecksum: book.Checksum,
                    cancellationToken);

                results.Add(result);
            }

            processed++;
        }

        progress?.Report(new AuditProgress
        {
            CurrentFile = null,
            FilesProcessed = processed,
            TotalFiles = totalBooks
        });

        return new CatalogVerificationResult
        {
            TotalBooks = totalBooks,
            VerifiedBooks = results.Count(r => r.Status == FileVerificationStatus.Valid),
            MissingBooks = results.Count(r => r.Status == FileVerificationStatus.Missing),
            CorruptBooks = results.Count(r => r.Status == FileVerificationStatus.Corrupt),
            Results = results,
            Duration = DateTime.UtcNow - startTime
        };
    }

    private static async Task<string> CalculateChecksumAsync(string filePath, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(filePath);
        using var sha256 = SHA256.Create();
        var hash = await sha256.ComputeHashAsync(stream, cancellationToken);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string? FindTextFile(EbookMetadata book)
    {
        // Try to find a text file path from the book's local file path
        // This is simplified - in reality, we'd check all format URLs
        // and map them to local paths
        return null; // TODO: Implement file path resolution from catalog
    }
}

