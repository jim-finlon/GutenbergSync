using GutenbergSync.Core.Catalog;
using GutenbergSync.Core.Configuration;
using GutenbergSync.Core.Metadata;
using Serilog;

namespace GutenbergSync.Core.Sync;

/// <summary>
/// Orchestrates the metadata-first sync strategy
/// </summary>
public sealed class SyncOrchestrator : ISyncOrchestrator
{
    private readonly IRsyncService _rsyncService;
    private readonly IRdfParser _rdfParser;
    private readonly ICatalogRepository _catalogRepository;
    private readonly ILogger _logger;

    public SyncOrchestrator(
        IRsyncService rsyncService,
        IRdfParser rdfParser,
        ICatalogRepository catalogRepository,
        ILogger logger)
    {
        _rsyncService = rsyncService;
        _rdfParser = rdfParser;
        _catalogRepository = catalogRepository;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<SyncOrchestrationResult> SyncAsync(
        SyncOrchestrationOptions options,
        IProgress<SyncOrchestrationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;

        try
        {
            // Ensure catalog is initialized
            await _catalogRepository.InitializeAsync(cancellationToken);

            // Phase 1: Sync metadata
            progress?.Report(new SyncOrchestrationProgress
            {
                Phase = "Metadata",
                Message = "Syncing RDF metadata files..."
            });

            var metadataResult = await SyncMetadataAsync(options, progress, cancellationToken);

            if (!metadataResult.Success)
            {
                // If cancelled, provide helpful message
                if (syncResult.WasCancelled)
                {
                    return new SyncOrchestrationResult
                    {
                        Success = false,
                        MetadataSync = metadataResult,
                        Duration = DateTime.UtcNow - startTime,
                        ErrorMessage = "Sync was cancelled. Run the same command again to resume from where it stopped."
                    };
                }

                return new SyncOrchestrationResult
                {
                    Success = false,
                    MetadataSync = metadataResult,
                    Duration = DateTime.UtcNow - startTime,
                    ErrorMessage = metadataResult.ErrorMessage ?? "Metadata sync failed"
                };
            }

            // Phase 2: Sync content (if not metadata-only)
            ContentSyncResult? contentResult = null;
            if (!options.MetadataOnly)
            {
                contentResult = await SyncContentAsync(options, progress, cancellationToken);
            }

            return new SyncOrchestrationResult
            {
                Success = true,
                MetadataSync = metadataResult,
                ContentSync = contentResult,
                Duration = DateTime.UtcNow - startTime
            };
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Sync orchestration failed");
            return new SyncOrchestrationResult
            {
                Success = false,
                Duration = DateTime.UtcNow - startTime,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <inheritdoc/>
    public async Task<MetadataSyncResult> SyncMetadataAsync(
        SyncOrchestrationOptions options,
        IProgress<SyncOrchestrationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;

        try
        {
            // Sync RDF files from gutenberg-epub module
            // RDF files are in each book's directory (e.g., 1/pg1.rdf, 2/pg2.rdf)
            // We sync the entire gutenberg-epub module and filter for .rdf files
            var rdfEndpoint = "aleph.gutenberg.org::gutenberg-epub/";
            var rdfTargetDir = Path.Combine(options.TargetDirectory, "gutenberg-epub");

            var rsyncOptions = new RsyncOptions
            {
                Include = new[] { "*/", "*.rdf" }, // Include directories and RDF files
                DryRun = options.DryRun,
                ShowProgress = true,
                TimeoutSeconds = 3600 // 1 hour timeout for metadata sync (can be large)
            };

            progress?.Report(new SyncOrchestrationProgress
            {
                Phase = "Metadata",
                Message = "Downloading RDF files...",
                ProgressPercent = 0
            });

            var syncResult = await _rsyncService.SyncAsync(
                rdfEndpoint,
                rdfTargetDir,
                rsyncOptions,
                null,
                cancellationToken);

            if (!syncResult.Success)
            {
                // If cancelled, this is expected - user can resume
                if (syncResult.WasCancelled)
                {
                    _logger.Information("Metadata sync cancelled. Partial files preserved. Run again to resume.");
                    return new MetadataSyncResult
                    {
                        Success = false,
                        RdfFilesSynced = (int)syncResult.FilesTransferred,
                        RecordsAdded = 0,
                        Duration = DateTime.UtcNow - startTime,
                        ErrorMessage = "Sync was cancelled. Run the same command again to resume."
                    };
                }

                return new MetadataSyncResult
                {
                    Success = false,
                    RdfFilesSynced = 0,
                    RecordsAdded = 0,
                    Duration = DateTime.UtcNow - startTime,
                    ErrorMessage = syncResult.ErrorMessage
                };
            }

            // Parse RDF files and build catalog
            progress?.Report(new SyncOrchestrationProgress
            {
                Phase = "Metadata",
                Message = "Parsing RDF files and building catalog...",
                ProgressPercent = 50
            });

            var recordsAdded = 0;
            var totalRdfFiles = Directory.Exists(rdfTargetDir) 
                ? Directory.EnumerateFiles(rdfTargetDir, "*.rdf", SearchOption.AllDirectories).Count() 
                : 0;
            var processedFiles = 0;

            if (totalRdfFiles > 0)
            {
                _logger.Information("Found {Count} RDF files to parse", totalRdfFiles);
            }

            // RDF files are in subdirectories like 1/pg1.rdf, 2/pg2.rdf, etc.
            // ParseDirectoryAsync will recursively search for .rdf files
            await foreach (var metadata in _rdfParser.ParseDirectoryAsync(rdfTargetDir, cancellationToken))
            {
                await _catalogRepository.UpsertAsync(metadata, cancellationToken);
                recordsAdded++;
                processedFiles++;

                // Update progress every 100 files
                if (totalRdfFiles > 0 && processedFiles % 100 == 0)
                {
                    progress?.Report(new SyncOrchestrationProgress
                    {
                        Phase = "Metadata",
                        Message = $"Parsing RDF files... {processedFiles}/{totalRdfFiles}",
                        ProgressPercent = 50 + (processedFiles / (double)totalRdfFiles * 50)
                    });
                }
            }

            progress?.Report(new SyncOrchestrationProgress
            {
                Phase = "Metadata",
                Message = "Metadata sync completed",
                ProgressPercent = 100
            });

            _logger.Information("Metadata sync completed: {RecordsAdded} records added", recordsAdded);

            return new MetadataSyncResult
            {
                Success = true,
                RdfFilesSynced = (int)syncResult.FilesTransferred,
                RecordsAdded = recordsAdded,
                Duration = DateTime.UtcNow - startTime
            };
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Metadata sync failed");
            return new MetadataSyncResult
            {
                Success = false,
                RdfFilesSynced = 0,
                RecordsAdded = 0,
                Duration = DateTime.UtcNow - startTime,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <inheritdoc/>
    public async Task<ContentSyncResult> SyncContentAsync(
        SyncOrchestrationOptions options,
        IProgress<SyncOrchestrationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        var filesSynced = 0L;
        var bytesTransferred = 0L;

        try
        {
            // Get all books from catalog to use as checklist
            var allBooks = await _catalogRepository.SearchAsync(
                new CatalogSearchOptions { Limit = null },
                cancellationToken);

            _logger.Information("Syncing content for {BookCount} books from catalog", allBooks.Count);

            // Determine include patterns based on preset
            var includePatterns = SyncPresets.GetPresetPatterns(options.Preset);

            // Sync main collection (gutenberg module)
            var mainEndpoint = "aleph.gutenberg.org::gutenberg";
            var mainTargetDir = Path.Combine(options.TargetDirectory, "gutenberg");

            var rsyncOptions = new RsyncOptions
            {
                Include = includePatterns.Length > 0 ? includePatterns : Array.Empty<string>(),
                DryRun = options.DryRun,
                ShowProgress = true
            };

            progress?.Report(new SyncOrchestrationProgress
            {
                Phase = "Content",
                Message = "Syncing main collection files...",
                ProgressPercent = 0
            });

            var mainSyncResult = await _rsyncService.SyncAsync(
                mainEndpoint,
                mainTargetDir,
                rsyncOptions,
                null,
                cancellationToken);

            if (mainSyncResult.Success)
            {
                filesSynced += mainSyncResult.FilesTransferred;
                bytesTransferred += mainSyncResult.BytesTransferred;
            }

            // Sync generated formats (gutenberg-epub module) if preset includes them
            if (options.Preset != "text-only" && options.Preset != "all-text")
            {
                progress?.Report(new SyncOrchestrationProgress
                {
                    Phase = "Content",
                    Message = "Syncing generated formats (EPUB, MOBI)...",
                    ProgressPercent = 50
                });

                var epubEndpoint = "aleph.gutenberg.org::gutenberg-epub";
                var epubTargetDir = Path.Combine(options.TargetDirectory, "gutenberg-epub");

                var epubSyncResult = await _rsyncService.SyncAsync(
                    epubEndpoint,
                    epubTargetDir,
                    rsyncOptions,
                    null,
                    cancellationToken);

                if (epubSyncResult.Success)
                {
                    filesSynced += epubSyncResult.FilesTransferred;
                    bytesTransferred += epubSyncResult.BytesTransferred;
                }
            }

            progress?.Report(new SyncOrchestrationProgress
            {
                Phase = "Content",
                Message = "Content sync completed",
                ProgressPercent = 100
            });

            _logger.Information("Content sync completed: {FilesSynced} files, {BytesTransferred} bytes",
                filesSynced, bytesTransferred);

            return new ContentSyncResult
            {
                FilesSynced = filesSynced,
                BytesTransferred = bytesTransferred,
                Duration = DateTime.UtcNow - startTime
            };
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Content sync failed");
            return new ContentSyncResult
            {
                FilesSynced = filesSynced,
                BytesTransferred = bytesTransferred,
                Duration = DateTime.UtcNow - startTime
            };
        }
    }
}

