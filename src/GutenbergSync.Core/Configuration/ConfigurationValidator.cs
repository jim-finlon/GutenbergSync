using System.IO;

namespace GutenbergSync.Core.Configuration;

/// <summary>
/// Validates application configuration
/// </summary>
public sealed class ConfigurationValidator : IConfigurationValidator
{
    /// <inheritdoc/>
    public Task<ConfigurationValidationResult> ValidateAsync(AppConfiguration config, CancellationToken cancellationToken = default)
    {
        var errors = new List<ValidationError>();
        var warnings = new List<ValidationWarning>();

        // Validate sync configuration
        ValidateSyncConfiguration(config.Sync, errors, warnings);

        // Validate catalog configuration
        ValidateCatalogConfiguration(config.Catalog, config.Sync, errors, warnings);

        // Validate extraction configuration
        ValidateExtractionConfiguration(config.Extraction, errors, warnings);

        // Validate logging configuration
        ValidateLoggingConfiguration(config.Logging, errors, warnings);

        return Task.FromResult(new ConfigurationValidationResult
        {
            IsValid = errors.Count == 0,
            Errors = errors,
            Warnings = warnings
        });
    }

    private static void ValidateSyncConfiguration(SyncConfiguration sync, List<ValidationError> errors, List<ValidationWarning> warnings)
    {
        if (string.IsNullOrWhiteSpace(sync.TargetDirectory))
        {
            errors.Add(new ValidationError
            {
                Path = "sync.targetDirectory",
                Message = "Target directory is required",
                SuggestedFix = "Set sync.targetDirectory to a valid directory path"
            });
            return;
        }

        // Check if target directory exists or can be created
        try
        {
            var dir = new DirectoryInfo(sync.TargetDirectory);

            if (!dir.Exists)
            {
                try
                {
                    dir.Create();
                    warnings.Add(new ValidationWarning
                    {
                        Path = "sync.targetDirectory",
                        Message = $"Target directory '{sync.TargetDirectory}' does not exist and will be created"
                    });
                }
                catch (Exception ex)
                {
                    errors.Add(new ValidationError
                    {
                        Path = "sync.targetDirectory",
                        Message = $"Cannot create target directory: {ex.Message}",
                        SuggestedFix = "Ensure the path is valid and you have write permissions"
                    });
                    return; // Can't continue validation if directory creation failed
                }
            }

            // Always check writability, whether directory existed or was just created
            if (!IsDirectoryWritable(dir))
            {
                errors.Add(new ValidationError
                {
                    Path = "sync.targetDirectory",
                    Message = "Target directory is not writable",
                    SuggestedFix = "Check directory permissions"
                });
            }
        }
        catch (Exception ex)
        {
            errors.Add(new ValidationError
            {
                Path = "sync.targetDirectory",
                Message = $"Invalid target directory path: {ex.Message}",
                SuggestedFix = "Provide a valid absolute or relative directory path"
            });
        }

        // Validate preset
        if (!string.IsNullOrWhiteSpace(sync.Preset))
        {
            var validPresets = new[] { "text-only", "text-epub", "all-text", "full" };
            if (!validPresets.Contains(sync.Preset, StringComparer.OrdinalIgnoreCase))
            {
                warnings.Add(new ValidationWarning
                {
                    Path = "sync.preset",
                    Message = $"Unknown preset '{sync.Preset}'. Valid presets: {string.Join(", ", validPresets)}"
                });
            }
        }

        // Validate timeout
        if (sync.TimeoutSeconds <= 0)
        {
            errors.Add(new ValidationError
            {
                Path = "sync.timeoutSeconds",
                Message = "Timeout must be greater than 0",
                SuggestedFix = "Set sync.timeoutSeconds to a positive value (e.g., 600)"
            });
        }
    }

    private static void ValidateCatalogConfiguration(CatalogConfiguration catalog, SyncConfiguration sync, List<ValidationError> errors, List<ValidationWarning> warnings)
    {
        if (!string.IsNullOrWhiteSpace(catalog.DatabasePath))
        {
            try
            {
                var dbPath = catalog.DatabasePath;
                var dbDir = Path.GetDirectoryName(dbPath);

                if (string.IsNullOrWhiteSpace(dbDir))
                {
                    // Relative path - check if it can be resolved
                    dbDir = Path.GetDirectoryName(Path.GetFullPath(dbPath));
                }

                if (!string.IsNullOrWhiteSpace(dbDir))
                {
                    var dir = new DirectoryInfo(dbDir);

                    if (!dir.Exists)
                    {
                        try
                        {
                            dir.Create();
                        }
                        catch (Exception ex)
                        {
                            errors.Add(new ValidationError
                            {
                                Path = "catalog.databasePath",
                                Message = $"Cannot create database directory: {ex.Message}",
                                SuggestedFix = "Ensure the directory path is valid and you have write permissions"
                            });
                            return; // Can't continue validation if directory creation failed
                        }
                    }

                    // Always check writability, whether directory existed or was just created
                    if (!IsDirectoryWritable(dir))
                    {
                        errors.Add(new ValidationError
                        {
                            Path = "catalog.databasePath",
                            Message = "Database directory is not writable",
                            SuggestedFix = "Check directory permissions"
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                errors.Add(new ValidationError
                {
                    Path = "catalog.databasePath",
                    Message = $"Invalid database path: {ex.Message}",
                    SuggestedFix = "Provide a valid file path or leave null to use default"
                });
            }
        }
    }

    private static void ValidateExtractionConfiguration(ExtractionConfiguration extraction, List<ValidationError> errors, List<ValidationWarning> warnings)
    {
        if (!string.IsNullOrWhiteSpace(extraction.OutputDirectory))
        {
            try
            {
                var dir = new DirectoryInfo(extraction.OutputDirectory);

                if (!dir.Exists)
                {
                    try
                    {
                        dir.Create();
                    }
                    catch (Exception ex)
                    {
                        errors.Add(new ValidationError
                        {
                            Path = "extraction.outputDirectory",
                            Message = $"Cannot create output directory: {ex.Message}",
                            SuggestedFix = "Ensure the path is valid and you have write permissions"
                        });
                        return; // Can't continue validation if directory creation failed
                    }
                }

                // Always check writability, whether directory existed or was just created
                if (!IsDirectoryWritable(dir))
                {
                    errors.Add(new ValidationError
                    {
                        Path = "extraction.outputDirectory",
                        Message = "Output directory is not writable",
                        SuggestedFix = "Check directory permissions"
                    });
                }
            }
            catch (Exception ex)
            {
                errors.Add(new ValidationError
                {
                    Path = "extraction.outputDirectory",
                    Message = $"Invalid output directory path: {ex.Message}",
                    SuggestedFix = "Provide a valid directory path"
                });
            }
        }

        if (extraction.DefaultChunkSizeWords <= 0)
        {
            errors.Add(new ValidationError
            {
                Path = "extraction.defaultChunkSizeWords",
                Message = "Chunk size must be greater than 0",
                SuggestedFix = "Set extraction.defaultChunkSizeWords to a positive value (e.g., 500)"
            });
        }

        if (extraction.DefaultChunkOverlapWords < 0)
        {
            errors.Add(new ValidationError
            {
                Path = "extraction.defaultChunkOverlapWords",
                Message = "Chunk overlap cannot be negative",
                SuggestedFix = "Set extraction.defaultChunkOverlapWords to 0 or greater"
            });
        }

        var validFormats = new[] { "json", "parquet", "arrow", "txt" };
        if (!validFormats.Contains(extraction.DefaultFormat, StringComparer.OrdinalIgnoreCase))
        {
            warnings.Add(new ValidationWarning
            {
                Path = "extraction.defaultFormat",
                Message = $"Unknown format '{extraction.DefaultFormat}'. Valid formats: {string.Join(", ", validFormats)}"
            });
        }
    }

    private static void ValidateLoggingConfiguration(LoggingConfiguration logging, List<ValidationError> errors, List<ValidationWarning> warnings)
    {
        var validLevels = new[] { "Trace", "Debug", "Information", "Warning", "Error", "Critical" };
        if (!validLevels.Contains(logging.Level, StringComparer.OrdinalIgnoreCase))
        {
            warnings.Add(new ValidationWarning
            {
                Path = "logging.level",
                Message = $"Unknown log level '{logging.Level}'. Valid levels: {string.Join(", ", validLevels)}"
            });
        }

        if (!string.IsNullOrWhiteSpace(logging.FilePath))
        {
            try
            {
                var logDir = Path.GetDirectoryName(logging.FilePath);
                if (!string.IsNullOrWhiteSpace(logDir))
                {
                    var dir = new DirectoryInfo(logDir);

                    if (!dir.Exists)
                    {
                        try
                        {
                            dir.Create();
                        }
                        catch (Exception ex)
                        {
                            errors.Add(new ValidationError
                            {
                                Path = "logging.filePath",
                                Message = $"Cannot create log directory: {ex.Message}",
                                SuggestedFix = "Ensure the directory path is valid and you have write permissions"
                            });
                            return; // Can't continue validation if directory creation failed
                        }
                    }

                    // Always check writability, whether directory existed or was just created
                    if (!IsDirectoryWritable(dir))
                    {
                        errors.Add(new ValidationError
                        {
                            Path = "logging.filePath",
                            Message = "Log directory is not writable",
                            SuggestedFix = "Check directory permissions"
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                errors.Add(new ValidationError
                {
                    Path = "logging.filePath",
                    Message = $"Invalid log file path: {ex.Message}",
                    SuggestedFix = "Provide a valid file path or leave null to disable file logging"
                });
            }
        }
    }

    private static bool IsDirectoryWritable(DirectoryInfo directory)
    {
        try
        {
            var testFile = Path.Combine(directory.FullName, Guid.NewGuid().ToString());
            File.Create(testFile).Dispose();
            File.Delete(testFile);
            return true;
        }
        catch
        {
            return false;
        }
    }
}

