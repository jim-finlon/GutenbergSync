using Microsoft.EntityFrameworkCore;
using GutenbergSync.Core.Configuration;
using GutenbergSync.Core.Catalog;
using GutenbergSync.Core.Infrastructure;
using GutenbergSync.Core.Metadata;
using GutenbergSync.Core.Sync;
using GutenbergSync.Core.Extraction;
using GutenbergSync.Web.Hubs;
using GutenbergSync.Web.Services;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Add core services (includes IConfigurationLoader, etc.)
builder.Services.AddGutenbergSyncCore();

// Now build temp provider to load config
var tempProvider = builder.Services.BuildServiceProvider();
var configLoader = tempProvider.GetRequiredService<IConfigurationLoader>();
var tempLoggerFactory = tempProvider.GetRequiredService<GutenbergSync.Core.Infrastructure.ILoggerFactory>();
var tempLogger = tempLoggerFactory.CreateLogger(new LoggingConfiguration { Level = "Information" });
AppConfiguration config;

try
{
    // Try multiple strategies to find config.json:
    // 1. Current working directory
    // 2. Parent directories (if running from src/GutenbergSync.Web/bin/Debug)
    // 3. Hardcoded absolute path as last resort
    var configPath = "config.json";
    var found = false;
    
    if (File.Exists(configPath))
    {
        configPath = Path.GetFullPath(configPath);
        found = true;
    }
    else
    {
        // Try parent directories (common when running from bin/Debug/net9.0)
        var currentDir = Directory.GetCurrentDirectory();
        var dir = new DirectoryInfo(currentDir);
        for (int i = 0; i < 5 && dir != null; i++)
        {
            var testPath = Path.Combine(dir.FullName, "config.json");
            if (File.Exists(testPath))
            {
                configPath = testPath;
                found = true;
                break;
            }
            dir = dir.Parent;
        }
    }
    
    // Last resort: hardcoded absolute path
    if (!found)
    {
        var absolutePath = "/home/jfinlon/Documents/Projects/Gutenberg Archive/config.json";
        if (File.Exists(absolutePath))
        {
            configPath = absolutePath;
            found = true;
        }
    }
    
    if (found && File.Exists(configPath))
    {
        tempLogger.Information("Loading config from: {ConfigPath}", configPath);
        config = await configLoader.LoadFromFileAsync(configPath);
        tempLogger.Information("Config loaded - TargetDirectory: {TargetDir}", config.Sync.TargetDirectory);
        // FORCE override to use correct database path
        config = config with
        {
            Sync = config.Sync with { TargetDirectory = "/mnt/workspace/gutenberg" },
            Catalog = config.Catalog with { DatabasePath = "/mnt/workspace/gutenberg/gutenberg.db" }
        };
        tempLogger.Information("FORCED override - TargetDirectory: {TargetDir}, DatabasePath: {DbPath}", 
            config.Sync.TargetDirectory, config.Catalog.DatabasePath);
    }
    else
    {
        tempLogger.Warning("Config file not found at {ConfigPath}, using defaults with hardcoded override", configPath);
        config = configLoader.CreateDefault();
        // FORCE correct path - don't use default UserProfile path
        config = config with
        {
            Sync = config.Sync with { TargetDirectory = "/mnt/workspace/gutenberg" },
            Catalog = config.Catalog with { DatabasePath = "/mnt/workspace/gutenberg/gutenberg.db" }
        };
        tempLogger.Warning("Using default config with FORCED paths - TargetDirectory: {TargetDir}, DatabasePath: {DbPath}", 
            config.Sync.TargetDirectory, config.Catalog.DatabasePath);
    }
}
catch (Exception ex)
{
    tempLogger.Warning(ex, "Failed to load config, using defaults with hardcoded override");
    config = configLoader.CreateDefault();
    // FORCE correct path - don't use default
    config = config with
    {
        Sync = config.Sync with { TargetDirectory = "/mnt/workspace/gutenberg" },
        Catalog = config.Catalog with { DatabasePath = "/mnt/workspace/gutenberg/gutenberg.db" }
    };
    tempLogger.Warning("Using hardcoded paths - TargetDirectory: {TargetDir}, DatabasePath: {DbPath}", 
        config.Sync.TargetDirectory, config.Catalog.DatabasePath);
}

// Register config (override the one from AddGutenbergSyncCore if needed)
var existingConfig = builder.Services.FirstOrDefault(s => s.ServiceType == typeof(AppConfiguration));
if (existingConfig != null)
{
    builder.Services.Remove(existingConfig);
}
builder.Services.AddSingleton(config);

// Remove existing CatalogRepository registration and re-register with correct config
var existingCatalogRepo = builder.Services.FirstOrDefault(s => s.ServiceType == typeof(ICatalogRepository));
if (existingCatalogRepo != null)
{
    builder.Services.Remove(existingCatalogRepo);
}
// Register CatalogDbContext in DI (Scoped - one per HTTP request)
builder.Services.AddDbContext<CatalogDbContext>(options =>
{
    const string dbPath = "/mnt/workspace/gutenberg/gutenberg.db";
    var connectionString = $"Data Source={dbPath};Cache=Shared;Pooling=False;";
    options.UseSqlite(connectionString);
    options.UseQueryTrackingBehavior(Microsoft.EntityFrameworkCore.QueryTrackingBehavior.NoTracking);
}, ServiceLifetime.Scoped);

// Re-register CatalogRepository - it will use the config we just registered
builder.Services.AddScoped<ICatalogRepository, CatalogRepository>();

// Configure logging
var loggerFactory = tempProvider.GetRequiredService<GutenbergSync.Core.Infrastructure.ILoggerFactory>();
var logger = loggerFactory.CreateLogger(config.Logging);
builder.Services.AddSingleton(logger);

// Add ASP.NET Core logging
builder.Services.AddLogging(builder =>
{
    builder.AddConsole();
    builder.AddDebug();
});

// Add SignalR
builder.Services.AddSignalR();

// Add CORS for local development
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Add API controllers
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    });

// Register web services
builder.Services.AddSingleton<ISyncProgressBroadcaster, SyncProgressBroadcaster>();
builder.Services.AddScoped<IEpubCopyService, EpubCopyService>();
builder.Services.AddScoped<WebSyncService>();

var app = builder.Build();

// Initialize catalog database on startup - use the SAME forced path
try
{
    using (var scope = app.Services.CreateScope())
    {
        logger.Information("=== Initializing catalog database on startup ===");
        var catalogRepo = scope.ServiceProvider.GetRequiredService<ICatalogRepository>();
        
        logger.Information("CatalogRepository instance obtained, calling InitializeAsync");
        await catalogRepo.InitializeAsync();
        logger.Information("InitializeAsync completed");
        
        // Test query to verify database has data
        // Note: This is done within the scope, so the repository can create its own context
        try
        {
            logger.Information("Testing database query...");
            var testStats = await catalogRepo.GetStatisticsAsync();
            logger.Information("SUCCESS: Catalog database - Found {Books} books, {Authors} authors, {Languages} languages", 
                testStats.TotalBooks, testStats.TotalAuthors, testStats.UniqueLanguages);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Database initialized but query FAILED: {Error}", ex.Message);
            logger.Error("Stack trace: {StackTrace}", ex.StackTrace);
            // DON'T let this exception propagate - it's just a test during startup
        }
        logger.Information("=== End catalog initialization ===");
    }
}
catch (Exception ex)
{
    logger.Error(ex, "CRITICAL: Failed to initialize catalog database on startup: {Error}", ex.Message);
    logger.Error("Stack trace: {StackTrace}", ex.StackTrace);
    // Don't fail startup, but log the error
}

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

app.UseCors();

// Map API routes and hubs FIRST - these must be registered before static files
app.MapControllers();
app.MapHub<SyncProgressHub>("/hubs/sync");

// Static files
app.UseDefaultFiles();
app.UseStaticFiles();

// Fallback to index.html for SPA routing
// Note: This will only match if no other route matched, so API routes take precedence
app.MapFallbackToFile("index.html");

app.Run();
