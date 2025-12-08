using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ICSharpCode.SharpZipLib.Zip;
using GutenbergSync.Core.Metadata;
using GutenbergSync.Core.Catalog;
using Serilog;

namespace GutenbergSync.Core.Extraction;

/// <summary>
/// Extracts and processes text from Project Gutenberg files
/// </summary>
public sealed class TextExtractor : ITextExtractor
{
    private readonly ICatalogRepository _catalogRepository;
    private readonly ILogger _logger;

    public TextExtractor(ICatalogRepository catalogRepository, ILogger logger)
    {
        _catalogRepository = catalogRepository;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<ExtractionResult> ExtractAsync(
        string sourceFilePath,
        TextExtractionOptions options,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(sourceFilePath))
        {
            return new ExtractionResult
            {
                SourceFilePath = sourceFilePath,
                Success = false,
                ErrorMessage = "Source file not found"
            };
        }

        try
        {
            // Extract text from file
            var text = await ExtractTextFromFileAsync(sourceFilePath, options, cancellationToken);
            if (string.IsNullOrWhiteSpace(text))
            {
                return new ExtractionResult
                {
                    SourceFilePath = sourceFilePath,
                    Success = false,
                    ErrorMessage = "No text extracted from file"
                };
            }

            // Get book metadata from catalog
            var bookId = ExtractBookIdFromPath(sourceFilePath);
            var metadata = bookId.HasValue
                ? await _catalogRepository.GetByIdAsync(bookId.Value, cancellationToken)
                : null;

            // Create book metadata for chunks
            var bookMetadata = metadata != null
                ? new BookMetadata
                {
                    BookId = metadata.BookId,
                    Title = metadata.Title,
                    Authors = metadata.Authors.Select(a => a.Name).ToList(),
                    LanguageIsoCode = metadata.LanguageIsoCode,
                    PublicationDate = metadata.PublicationDate,
                    Subjects = metadata.Subjects.ToList()
                }
                : null;

            // Chunk text if needed
            var chunks = options.ChunkSizeWords > 0
                ? ChunkText(text, bookMetadata, options)
                : new List<TextChunk>
                {
                    new TextChunk
                    {
                        BookId = bookMetadata?.BookId ?? 0,
                        Title = bookMetadata?.Title ?? "Unknown",
                        Authors = bookMetadata?.Authors ?? [],
                        LanguageIsoCode = bookMetadata?.LanguageIsoCode,
                        PublicationDate = bookMetadata?.PublicationDate,
                        Subjects = bookMetadata?.Subjects ?? [],
                        ChunkIndex = 0,
                        Text = text,
                        WordCount = CountWords(text),
                        CharacterCount = text.Length
                    }
                };

            // Validate chunks if requested
            if (options.ValidateChunks)
            {
                chunks = ValidateChunks(chunks);
            }

            return new ExtractionResult
            {
                SourceFilePath = sourceFilePath,
                Chunks = chunks,
                BookMetadata = bookMetadata,
                Success = true,
                TotalWordCount = chunks.Sum(c => c.WordCount),
                TotalCharacterCount = chunks.Sum(c => c.CharacterCount)
            };
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error extracting text from {FilePath}", sourceFilePath);
            return new ExtractionResult
            {
                SourceFilePath = sourceFilePath,
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <inheritdoc/>
    public async Task ExtractBatchAsync(
        IEnumerable<string> sourceFiles,
        string outputDirectory,
        TextExtractionOptions options,
        IProgress<BatchProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var files = sourceFiles.ToList();
        var totalFiles = files.Count;
        var processed = 0;
        var totalChunks = 0L;

        if (!Directory.Exists(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        foreach (var file in files)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            progress?.Report(new BatchProgress
            {
                TotalFiles = totalFiles,
                FilesProcessed = processed,
                CurrentFile = file,
                TotalChunks = totalChunks
            });

            try
            {
                var result = await ExtractAsync(file, options, cancellationToken);
                if (result.Success && result.Chunks.Count > 0)
                {
                    await WriteChunksToFileAsync(result, outputDirectory, options, cancellationToken);
                    totalChunks += result.Chunks.Count;
                }
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, "Failed to extract {File}", file);
            }

            processed++;
        }

        progress?.Report(new BatchProgress
        {
            TotalFiles = totalFiles,
            FilesProcessed = processed,
            CurrentFile = null,
            TotalChunks = totalChunks
        });
    }

    /// <inheritdoc/>
    public Task<ExtractionPreview> PreviewExtractionAsync(
        IEnumerable<string> sourceFiles,
        TextExtractionOptions options,
        CancellationToken cancellationToken = default)
    {
        var files = sourceFiles.ToList();
        var filesToProcess = new List<string>();
        var estimatedChunks = 0L;
        var estimatedSize = 0L;

        foreach (var file in files)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            if (!File.Exists(file))
                continue;

            try
            {
                // Quick estimate based on file size
                var fileInfo = new FileInfo(file);
                var estimatedWords = fileInfo.Length / 5; // Rough estimate: 5 bytes per word
                var chunksForFile = options.ChunkSizeWords > 0
                    ? (int)Math.Ceiling(estimatedWords / (double)options.ChunkSizeWords)
                    : 1;

                estimatedChunks += chunksForFile;
                estimatedSize += fileInfo.Length;
                filesToProcess.Add(file);
            }
            catch
            {
                // Skip files we can't analyze
            }
        }

        return Task.FromResult(new ExtractionPreview
        {
            TotalFiles = files.Count,
            EstimatedChunks = estimatedChunks,
            EstimatedOutputSizeBytes = estimatedSize,
            SkippedFiles = files.Count - filesToProcess.Count,
            FilesToProcess = filesToProcess
        });
    }

    private async Task<string> ExtractTextFromFileAsync(string filePath, TextExtractionOptions options, CancellationToken cancellationToken)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();

        return extension switch
        {
            ".txt" => await ExtractFromTextFileAsync(filePath, options, cancellationToken),
            ".zip" => await ExtractFromZipFileAsync(filePath, options, cancellationToken),
            ".html" or ".htm" => await ExtractFromHtmlFileAsync(filePath, options, cancellationToken),
            _ => throw new NotSupportedException($"File format not supported: {extension}")
        };
    }

    private async Task<string> ExtractFromTextFileAsync(string filePath, TextExtractionOptions options, CancellationToken cancellationToken)
    {
        // Try to detect encoding
        var encoding = DetectEncoding(filePath) ?? Encoding.UTF8;

        var text = await File.ReadAllTextAsync(filePath, encoding, cancellationToken);

        if (options.NormalizeEncoding && encoding != Encoding.UTF8)
        {
            text = Encoding.UTF8.GetString(Encoding.Convert(encoding, Encoding.UTF8, encoding.GetBytes(text)));
        }

        if (options.StripHeaders)
        {
            text = StripGutenbergMarkers(text);
        }

        return text;
    }

    private async Task<string> ExtractFromZipFileAsync(string filePath, TextExtractionOptions options, CancellationToken cancellationToken)
    {
        await using var fileStream = File.OpenRead(filePath);
        using var zipFile = new ZipFile(fileStream);

        foreach (ZipEntry entry in zipFile)
        {
            if (!entry.IsFile)
                continue;

            var entryName = entry.Name.ToLowerInvariant();
            if (entryName.EndsWith(".txt"))
            {
                await using var entryStream = zipFile.GetInputStream(entry);
                using var reader = new StreamReader(entryStream, Encoding.UTF8, true);
                var text = await reader.ReadToEndAsync();

                if (options.NormalizeEncoding)
                {
                    text = Encoding.UTF8.GetString(Encoding.UTF8.GetBytes(text));
                }

                if (options.StripHeaders)
                {
                    text = StripGutenbergMarkers(text);
                }

                return text;
            }
        }

        throw new InvalidOperationException("No .txt file found in zip archive");
    }

    private async Task<string> ExtractFromHtmlFileAsync(string filePath, TextExtractionOptions options, CancellationToken cancellationToken)
    {
        var html = await File.ReadAllTextAsync(filePath, Encoding.UTF8, cancellationToken);

        // Basic HTML tag removal (simplified - could use HtmlAgilityPack for better parsing)
        var text = Regex.Replace(html, "<[^>]+>", " ");
        text = System.Net.WebUtility.HtmlDecode(text);
        text = Regex.Replace(text, @"\s+", " ");

        if (options.StripHeaders)
        {
            text = StripGutenbergMarkers(text);
        }

        return text.Trim();
    }

    private static string StripGutenbergMarkers(string text)
    {
        var lines = text.Split('\n');
        var startIndex = 0;
        var endIndex = lines.Length;

        // Find start marker
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (GutenbergMarkers.StartMarkers.Any(marker => line.Contains(marker, StringComparison.OrdinalIgnoreCase)))
            {
                startIndex = i + 1;
                break;
            }
        }

        // Find end marker
        for (int i = lines.Length - 1; i >= startIndex; i--)
        {
            var line = lines[i].Trim();
            if (GutenbergMarkers.EndMarkers.Any(marker => line.Contains(marker, StringComparison.OrdinalIgnoreCase)))
            {
                endIndex = i;
                break;
            }
        }

        if (startIndex >= endIndex)
        {
            // Markers not found or invalid, return original text
            return text;
        }

        return string.Join("\n", lines.Skip(startIndex).Take(endIndex - startIndex));
    }

    private List<TextChunk> ChunkText(string text, BookMetadata? bookMetadata, TextExtractionOptions options)
    {
        var words = text.Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        var chunks = new List<TextChunk>();
        var chunkSize = options.ChunkSizeWords;
        var overlap = options.ChunkOverlapWords;

        for (int i = 0; i < words.Length; i += chunkSize - overlap)
        {
            var chunkWords = words.Skip(i).Take(chunkSize).ToArray();
            var chunkText = string.Join(" ", chunkWords);

            chunks.Add(new TextChunk
            {
                BookId = bookMetadata?.BookId ?? 0,
                Title = bookMetadata?.Title ?? "Unknown",
                Authors = bookMetadata?.Authors ?? [],
                LanguageIsoCode = bookMetadata?.LanguageIsoCode,
                PublicationDate = bookMetadata?.PublicationDate,
                Subjects = bookMetadata?.Subjects ?? [],
                ChunkIndex = chunks.Count,
                Text = chunkText,
                WordCount = chunkWords.Length,
                CharacterCount = chunkText.Length
            });

            // Don't overlap on last chunk
            if (i + chunkSize >= words.Length)
                break;
        }

        return chunks;
    }

    private static List<TextChunk> ValidateChunks(IReadOnlyList<TextChunk> chunks)
    {
        return chunks
            .Where(chunk => !string.IsNullOrWhiteSpace(chunk.Text))
            .Where(chunk => chunk.WordCount > 10) // Minimum word count
            .Where(chunk => chunk.CharacterCount < 100000) // Maximum character count
            .ToList();
    }

    private static int CountWords(string text)
    {
        return text.Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries).Length;
    }

    private static Encoding? DetectEncoding(string filePath)
    {
        try
        {
            using var reader = new StreamReader(filePath, Encoding.UTF8, true);
            _ = reader.ReadToEnd();
            return reader.CurrentEncoding;
        }
        catch
        {
            return null;
        }
    }

    private static int? ExtractBookIdFromPath(string filePath)
    {
        // Try to extract book ID from path (e.g., /1/2/3/4/12345/12345.txt or pg12345.txt)
        var fileName = Path.GetFileNameWithoutExtension(filePath);
        var match = Regex.Match(fileName, @"(\d+)");
        if (match.Success && int.TryParse(match.Groups[1].Value, out var bookId))
        {
            return bookId;
        }

        // Try from directory path
        var dirMatch = Regex.Match(filePath, @"/\d+/\d+/\d+/\d+/(\d+)/");
        if (dirMatch.Success && int.TryParse(dirMatch.Groups[1].Value, out var dirBookId))
        {
            return dirBookId;
        }

        return null;
    }

    private async Task WriteChunksToFileAsync(
        ExtractionResult result,
        string outputDirectory,
        TextExtractionOptions options,
        CancellationToken cancellationToken)
    {
        if (result.BookMetadata == null || result.Chunks.Count == 0)
            return;

        var bookId = result.BookMetadata.BookId;
        var extension = options.OutputFormat.ToLowerInvariant() switch
        {
            "json" => ".json",
            "txt" => ".txt",
            _ => ".json"
        };

        var outputPath = Path.Combine(outputDirectory, $"book_{bookId}_chunks{extension}");

        if (options.OutputFormat.ToLowerInvariant() == "json")
        {
            var json = JsonSerializer.Serialize(result.Chunks, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            if (options.CompressOutput)
            {
                outputPath += ".gz";
                await using var fileStream = File.Create(outputPath);
                await using var gzipStream = new System.IO.Compression.GZipStream(fileStream, System.IO.Compression.CompressionLevel.Optimal);
                await using var writer = new StreamWriter(gzipStream, Encoding.UTF8);
                await writer.WriteAsync(json);
            }
            else
            {
                await File.WriteAllTextAsync(outputPath, json, cancellationToken);
            }
        }
        else if (options.OutputFormat.ToLowerInvariant() == "txt")
        {
            var sb = new StringBuilder();
            foreach (var chunk in result.Chunks)
            {
                sb.AppendLine($"=== Chunk {chunk.ChunkIndex} ===");
                if (result.BookMetadata != null)
                {
                    sb.AppendLine($"Book: {result.BookMetadata.Title}");
                    sb.AppendLine($"Authors: {string.Join(", ", result.BookMetadata.Authors)}");
                }
                sb.AppendLine(chunk.Text);
                sb.AppendLine();
            }

            await File.WriteAllTextAsync(outputPath, sb.ToString(), cancellationToken);
        }

        _logger.Debug("Wrote {ChunkCount} chunks to {OutputPath}", result.Chunks.Count, outputPath);
    }
}

