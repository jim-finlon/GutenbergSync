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
                return new SyncOrchestrationResult
                {
                    Success = false,
                    MetadataSync = metadataResult,
                    Duration = DateTime.UtcNow - startTime,
                    ErrorMessage = "Metadata sync failed"
                };
            }

            // Phase 2: Sync content (if not metadata-only)
            ContentSyncResult? contentResult = null;
            if (!options.MetadataOnly)
            {
                progress?.Report(new SyncOrchestrationProgress
                {
                    Phase = "Content",
                    Message = "Syncing content files..."
                });

                // TODO: Implement content sync using catalog as checklist
                _logger.Information("Content sync not yet implemented - using catalog as checklist");
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
            // Sync RDF files from cache/epub directories
            var rdfEndpoint = "aleph.gutenberg.org::gutenberg-epub/cache/epub";
            var rdfTargetDir = Path.Combine(options.TargetDirectory, "cache", "epub");

            var rsyncOptions = new RsyncOptions
            {
                Include = new[] { "*.rdf" },
                DryRun = options.DryRun,
                ShowProgress = true
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
            await foreach (var metadata in _rdfParser.ParseDirectoryAsync(rdfTargetDir, cancellationToken))
            {
                await _catalogRepository.UpsertAsync(metadata, cancellationToken);
                recordsAdded++;
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
}

