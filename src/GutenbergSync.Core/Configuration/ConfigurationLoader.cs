using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace GutenbergSync.Core.Configuration;

/// <summary>
/// Loads application configuration from files and environment variables
/// </summary>
public sealed class ConfigurationLoader : IConfigurationLoader
{
    private readonly IConfigurationValidator _validator;

    public ConfigurationLoader(IConfigurationValidator validator)
    {
        _validator = validator;
    }

    /// <inheritdoc/>
    public async Task<AppConfiguration> LoadFromFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Configuration file not found: {filePath}");
        }

        var json = await File.ReadAllTextAsync(filePath, cancellationToken);
        var config = JsonSerializer.Deserialize<AppConfiguration>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip
        });

        if (config == null)
        {
            throw new InvalidOperationException($"Failed to deserialize configuration from {filePath}");
        }

        return config;
    }

    /// <inheritdoc/>
    public async Task<AppConfiguration> LoadAsync(string? filePath = null, CancellationToken cancellationToken = default)
    {
        AppConfiguration config;

        if (string.IsNullOrWhiteSpace(filePath))
        {
            // Try default locations
            var defaultPaths = new[]
            {
                "config.json",
                "gutenberg-sync.json",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".gutenberg-sync", "config.json")
            };

            filePath = defaultPaths.FirstOrDefault(File.Exists);
            
            if (filePath == null)
            {
                // No config file found, use defaults
                config = CreateDefault();
            }
            else
            {
                config = await LoadFromFileAsync(filePath, cancellationToken);
            }
        }
        else
        {
            config = await LoadFromFileAsync(filePath, cancellationToken);
        }

        // Apply environment variable overrides
        ApplyEnvironmentOverrides(config);

        return config;
    }

    /// <inheritdoc/>
    public AppConfiguration CreateDefault()
    {
        return new AppConfiguration
        {
            Sync = new SyncConfiguration
            {
                TargetDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "gutenberg"),
                Preset = "text-epub",
                Mirrors = new List<MirrorEndpoint>
                {
                    new() { Host = "aleph.gutenberg.org", Module = "gutenberg", Priority = 1, Region = "US" },
                    new() { Host = "aleph.gutenberg.org", Module = "gutenberg-epub", Priority = 1, Region = "US" },
                    new() { Host = "ftp.ibiblio.org", Module = "gutenberg", Priority = 2, Region = "US" }
                },
                Include = new List<string> { "*/", "*.txt", "*.epub", "*.zip" },
                Exclude = new List<string> { "*/old/*", "*.mp3", "*.ogg" },
                MaxFileSizeMb = 50,
                BandwidthLimitKbps = null,
                DeleteRemoved = false,
                TimeoutSeconds = 600
            },
            Catalog = new CatalogConfiguration
            {
                DatabasePath = null, // Will default to {targetDirectory}/gutenberg.db
                AutoRebuildOnSync = true,
                VerifyAfterSync = true,
                AuditScanIntervalDays = 7
            },
            Extraction = new ExtractionConfiguration
            {
                OutputDirectory = null,
                StripHeaders = true,
                NormalizeEncoding = true,
                DefaultChunkSizeWords = 500,
                DefaultChunkOverlapWords = 50,
                Incremental = true,
                ValidateChunks = true,
                DefaultFormat = "json",
                CompressOutput = false
            },
            Logging = new LoggingConfiguration
            {
                Level = "Information",
                FilePath = null,
                RetainDays = 30
            }
        };
    }

    private static void ApplyEnvironmentOverrides(AppConfiguration config)
    {
        // Sync configuration
        var targetDir = Environment.GetEnvironmentVariable("GUTENBERG_SYNC_TARGET_DIRECTORY");
        if (!string.IsNullOrWhiteSpace(targetDir))
        {
            config = config with
            {
                Sync = config.Sync with { TargetDirectory = targetDir }
            };
        }

        var bandwidth = Environment.GetEnvironmentVariable("GUTENBERG_SYNC_BANDWIDTH_LIMIT_KBPS");
        if (!string.IsNullOrWhiteSpace(bandwidth) && int.TryParse(bandwidth, out var bandwidthValue))
        {
            config = config with
            {
                Sync = config.Sync with { BandwidthLimitKbps = bandwidthValue }
            };
        }

        // Catalog configuration
        var dbPath = Environment.GetEnvironmentVariable("GUTENBERG_CATALOG_DATABASE_PATH");
        if (!string.IsNullOrWhiteSpace(dbPath))
        {
            config = config with
            {
                Catalog = config.Catalog with { DatabasePath = dbPath }
            };
        }

        // Logging configuration
        var logLevel = Environment.GetEnvironmentVariable("GUTENBERG_LOGGING_LEVEL");
        if (!string.IsNullOrWhiteSpace(logLevel))
        {
            config = config with
            {
                Logging = config.Logging with { Level = logLevel }
            };
        }

        var logPath = Environment.GetEnvironmentVariable("GUTENBERG_LOGGING_FILE_PATH");
        if (!string.IsNullOrWhiteSpace(logPath))
        {
            config = config with
            {
                Logging = config.Logging with { FilePath = logPath }
            };
        }
    }
}