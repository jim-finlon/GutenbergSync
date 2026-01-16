using Microsoft.AspNetCore.Mvc;
using GutenbergSync.Core.Catalog;
using GutenbergSync.Core.Configuration;
using GutenbergSync.Core.Sync;
using GutenbergSync.Web.Models;
using GutenbergSync.Web.Services;

namespace GutenbergSync.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ApiController : ControllerBase
{
    private readonly ICatalogRepository _catalog;
    private readonly WebSyncService _syncService;
    private readonly IEpubCopyService _epubCopy;
    private readonly AppConfiguration _config;
    private readonly ILogger<ApiController> _logger;

    public ApiController(
        ICatalogRepository catalog,
        WebSyncService syncService,
        IEpubCopyService epubCopy,
        AppConfiguration config,
        ILoggerFactory loggerFactory)
    {
        _catalog = catalog;
        _syncService = syncService;
        _epubCopy = epubCopy;
        _config = config;
        _logger = loggerFactory.CreateLogger<ApiController>();
    }

    [HttpGet("statistics")]
    public async Task<ActionResult> GetStatistics(CancellationToken cancellationToken)
    {
        // Write to both logger AND console to ensure we see it
        _logger.LogInformation("=== API GetStatistics CALLED ===");
        Console.WriteLine("=== API GetStatistics CALLED ===");
        
        try
        {
            _logger.LogInformation("API GetStatistics: About to call _catalog.GetStatisticsAsync");
            Console.WriteLine("API GetStatistics: About to call _catalog.GetStatisticsAsync");
            
            var stats = await _catalog.GetStatisticsAsync(cancellationToken);
            
            _logger.LogInformation("API GetStatistics: SUCCESS - Books: {Books}, Authors: {Authors}", 
                stats.TotalBooks, stats.TotalAuthors);
            Console.WriteLine($"API GetStatistics: SUCCESS - Books: {stats.TotalBooks}, Authors: {stats.TotalAuthors}");
            
            var response = new
            {
                totalBooks = stats.TotalBooks,
                totalAuthors = stats.TotalAuthors,
                uniqueLanguages = stats.UniqueLanguages,
                uniqueSubjects = stats.UniqueSubjects,
                totalFileSizeBytes = stats.TotalFileSizeBytes
            };
            
            _logger.LogInformation("API GetStatistics: Returning Ok response");
            Console.WriteLine("API GetStatistics: Returning Ok response");
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "API GetStatistics: EXCEPTION - Type: {Type}, Message: {Message}", 
                ex.GetType().FullName, ex.Message);
            Console.WriteLine($"API GetStatistics: EXCEPTION - {ex.GetType().FullName}: {ex.Message}");
            Console.WriteLine($"API GetStatistics: Stack trace: {ex.StackTrace}");
            if (ex.InnerException != null)
            {
                _logger.LogError("API GetStatistics: Inner exception: {InnerType} - {InnerMessage}", 
                    ex.InnerException.GetType().FullName, ex.InnerException.Message);
                Console.WriteLine($"API GetStatistics: Inner exception: {ex.InnerException.GetType().FullName}: {ex.InnerException.Message}");
            }
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpPost("search")]
    public async Task<ActionResult> Search([FromBody] SearchRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var options = new CatalogSearchOptions
            {
                Query = request.Query,
                Author = request.Author,
                Language = request.Language,
                Limit = request.Limit ?? 50,
                Offset = request.Offset ?? 0
            };

            var results = await _catalog.SearchAsync(options, cancellationToken);
            return Ok(new { results, count = results.Count });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in search API");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpPost("sync/start")]
    public async Task<ActionResult> StartSync(CancellationToken cancellationToken)
    {
        try
        {
            // Run sync in background
            _ = Task.Run(async () =>
            {
                try
                {
                    await _syncService.StartSyncAsync(cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Background sync failed");
                }
            }, cancellationToken);

            return Ok(new { message = "Sync started" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting sync");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpPost("epub/copy")]
    public async Task<ActionResult> CopyEpub([FromBody] CopyEpubRequest request)
    {
        try
        {
            var success = await _epubCopy.CopyEpubAsync(request.BookId, request.DestinationPath);
            if (success)
            {
                return Ok(new { message = "EPUB copied successfully" });
            }
            return NotFound(new { error = "EPUB not found" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error copying EPUB");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpGet("browse")]
    public async Task<ActionResult> BrowseDirectories([FromQuery] string? path = null)
    {
        await Task.CompletedTask; // Make it truly async
        try
        {
            // Default to home directory if no path provided
            var basePath = path ?? "/home/jfinlon";
            
            // Security: Only allow browsing under /home/jfinlon
            if (!basePath.StartsWith("/home/jfinlon"))
            {
                return BadRequest(new { error = "Access denied. Can only browse /home/jfinlon directory." });
            }
            
            if (!Directory.Exists(basePath))
            {
                return NotFound(new { error = "Directory not found" });
            }
            
            var directories = Directory.GetDirectories(basePath)
                .Select(d => new
                {
                    name = Path.GetFileName(d),
                    path = d,
                    isDirectory = true
                })
                .OrderBy(d => d.name)
                .ToList();
            
            var parentPath = Directory.GetParent(basePath)?.FullName;
            // Only show parent if it's still within /home/jfinlon (don't allow going above home)
            if (parentPath != null && parentPath.StartsWith("/home/jfinlon") && parentPath.Length >= "/home/jfinlon".Length)
            {
                directories.Insert(0, new
                {
                    name = "..",
                    path = parentPath,
                    isDirectory = true
                });
            }
            
            return Ok(new
            {
                currentPath = basePath,
                directories = directories
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error browsing directories");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    public class CopyEpubRequest
    {
        public int BookId { get; set; }
        public string DestinationPath { get; set; } = "";
    }
}

