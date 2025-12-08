using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using GutenbergSync.Core.Extraction;
using Serilog;

namespace GutenbergSync.Cli.Commands;

/// <summary>
/// Command for extracting text from ebooks
/// </summary>
public sealed class ExtractCommand
{
    public static Command Create(IServiceProvider serviceProvider)
    {
        var command = new Command("extract", "Extract text from ebooks for RAG ingestion");

        var inputOption = new Option<string[]>(
            aliases: new[] { "--input", "-i" },
            description: "Input file(s) or directory to extract from")
        {
            IsRequired = true,
            AllowMultipleArgumentsPerToken = true
        };

        var outputOption = new Option<string>(
            aliases: new[] { "--output", "-o" },
            description: "Output directory for extracted chunks")
        {
            IsRequired = true
        };

        var chunkSizeOption = new Option<int>(
            "--chunk-size",
            description: "Chunk size in words",
            getDefaultValue: () => 500);

        var chunkOverlapOption = new Option<int>(
            "--chunk-overlap",
            description: "Chunk overlap in words",
            getDefaultValue: () => 50);

        var formatOption = new Option<string>(
            "--format",
            description: "Output format (json, txt)",
            getDefaultValue: () => "json");

        var dryRunOption = new Option<bool>(
            "--dry-run",
            description: "Preview extraction without processing files");

        command.AddOption(inputOption);
        command.AddOption(outputOption);
        command.AddOption(chunkSizeOption);
        command.AddOption(chunkOverlapOption);
        command.AddOption(formatOption);
        command.AddOption(dryRunOption);

        command.SetHandler(async (inputs, output, chunkSize, chunkOverlap, format, dryRun) =>
        {
            var logger = serviceProvider.GetRequiredService<ILogger>();
            var extractor = serviceProvider.GetRequiredService<ITextExtractor>();

            try
            {
                var options = new TextExtractionOptions
                {
                    ChunkSizeWords = chunkSize,
                    ChunkOverlapWords = chunkOverlap,
                    OutputFormat = format,
                    StripHeaders = true,
                    NormalizeEncoding = true,
                    ValidateChunks = true
                };

                if (dryRun)
                {
                    var preview = await extractor.PreviewExtractionAsync(inputs, options);
                    logger.Information("Preview: {TotalFiles} files, {Chunks} estimated chunks", 
                        preview.TotalFiles, preview.EstimatedChunks);
                }
                else
                {
                    await extractor.ExtractBatchAsync(inputs, output, options, null);
                    logger.Information("Extraction completed");
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Extraction failed");
                Environment.ExitCode = 1;
            }
        }, inputOption, outputOption, chunkSizeOption, chunkOverlapOption, formatOption, dryRunOption);

        return command;
    }
}

