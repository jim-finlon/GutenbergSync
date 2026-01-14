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
                // Check if it was cancelled based on error message
                var wasCancelled = metadataResult.ErrorMessage?.Contains("cancelled") == true || 
                                  metadataResult.ErrorMessage?.Contains("resume") == true;

                return new SyncOrchestrationResult
                {
                    Success = false,
                    MetadataSync = metadataResult,
                    Duration = DateTime.UtcNow - startTime,
                    ErrorMessage = wasCancelled 
                        ? "Sync was cancelled. Run the same command again to resume from where it stopped."
                        : metadataResult.ErrorMessage ?? "Metadata sync failed"
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
                TimeoutSeconds = options.TimeoutSeconds ?? 3600 // 1 hour default for metadata sync
            };

            progress?.Report(new SyncOrchestrationProgress
            {
                Phase = "Metadata",
                Message = "Downloading RDF files...",
                ProgressPercent = 0
            });

            // Create progress handler for rsync that reports to orchestration progress
            IProgress<SyncProgress>? rsyncProgress = null;
            if (progress != null)
            {
                rsyncProgress = new Progress<SyncProgress>(p =>
                {
                    // Convert rsync progress to orchestration progress
                    var percent = p.ProgressPercent ?? 0;
                    string message;
                    
                    if (p.CurrentFile != null && !p.CurrentFile.Contains("Building") && !p.CurrentFile.Contains("Scanning"))
                    {
                        // Actual file transfer - keep message short, file name will be shown separately
                        var fileName = Path.GetFileName(p.CurrentFile);
                        message = $"Downloading... ({p.FilesTransferred} files)";
                        
                        // Use actual progress percentage from rsync
                        percent = p.ProgressPercent ?? 0;
                        
                        // Don't add MB info to message - it makes it too long and pushes bars around
                        // The file name is shown on a separate line above the progress bars
                    }
                    else if (p.CurrentFile != null)
                    {
                        // Building file list or scanning
                        message = p.CurrentFile;
                        if (p.TotalFiles.HasValue)
                        {
                            message += $" ({p.TotalFiles} files found)";
                        }
                    }
                    else
                    {
                        message = "Downloading RDF files...";
                    }
                    
                    // Report progress - Progress<T> handles thread marshaling
                    // Always report progress percentage if we have it (even if 0, so bars show activity)
                    progress.Report(new SyncOrchestrationProgress
                    {
                        Phase = "Metadata",
                        Message = message,
                        ProgressPercent = percent >= 0 ? percent : null,
                        CurrentFile = p.CurrentFile != null && !p.CurrentFile.Contains("Building") && !p.CurrentFile.Contains("Scanning")
                            ? Path.GetFileName(p.CurrentFile)
                            : null
                    });
                });
            }

            var syncResult = await _rsyncService.SyncAsync(
                rdfEndpoint,
                rdfTargetDir,
                rsyncOptions,
                rsyncProgress,
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
                ShowProgress = true,
                TimeoutSeconds = options.TimeoutSeconds ?? 0 // No timeout by default for content sync (can take hours)
            };

            progress?.Report(new SyncOrchestrationProgress
            {
                Phase = "Content",
                Message = "Syncing main collection files...",
                ProgressPercent = 0
            });

            // Create progress handler for main collection sync
            IProgress<SyncProgress>? mainRsyncProgress = null;
            if (progress != null)
            {
                mainRsyncProgress = new Progress<SyncProgress>(p =>
                {
                    // Use actual progress percentage from rsync
                    var percent = p.ProgressPercent ?? 0;
                    // Keep message short - file name shown separately above progress bars
                    var message = p.CurrentFile != null && !p.CurrentFile.Contains("Building") && !p.CurrentFile.Contains("Scanning")
                        ? $"Downloading... ({p.FilesTransferred} files)"
                        : p.CurrentFile ?? "Syncing main collection files...";
                    
                    // Don't add MB info - it makes message too long and pushes bars around
                    
                    progress.Report(new SyncOrchestrationProgress
                    {
                        Phase = "Content",
                        Message = message,
                        ProgressPercent = percent >= 0 ? percent : null,
                        CurrentFile = p.CurrentFile != null && !p.CurrentFile.Contains("Building") && !p.CurrentFile.Contains("Scanning")
                            ? Path.GetFileName(p.CurrentFile)
                            : null
                    });
                });
            }

            var mainSyncResult = await _rsyncService.SyncAsync(
                mainEndpoint,
                mainTargetDir,
                rsyncOptions,
                mainRsyncProgress,
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

            // Create progress handler for EPUB sync
            IProgress<SyncProgress>? epubRsyncProgress = null;
            if (progress != null)
            {
                epubRsyncProgress = new Progress<SyncProgress>(p =>
                {
                    // Use actual progress percentage from rsync
                    var percent = p.ProgressPercent ?? 0;
                    // Keep message short - file name shown separately above progress bars
                    var message = p.CurrentFile != null && !p.CurrentFile.Contains("Building") && !p.CurrentFile.Contains("Scanning")
                        ? $"Downloading... ({p.FilesTransferred} files)"
                        : p.CurrentFile ?? "Syncing generated formats (EPUB, MOBI)...";
                    
                    // Don't add MB info - it makes message too long and pushes bars around
                    
                    progress.Report(new SyncOrchestrationProgress
                    {
                        Phase = "Content",
                        Message = message,
                        ProgressPercent = percent >= 0 ? (50 + (percent * 0.5)) : null, // EPUB sync is second half of content phase
                        CurrentFile = p.CurrentFile != null && !p.CurrentFile.Contains("Building") && !p.CurrentFile.Contains("Scanning")
                            ? Path.GetFileName(p.CurrentFile)
                            : null
                    });
                });
            }

            var epubSyncResult = await _rsyncService.SyncAsync(
                epubEndpoint,
                epubTargetDir,
                rsyncOptions,
                epubRsyncProgress,
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

