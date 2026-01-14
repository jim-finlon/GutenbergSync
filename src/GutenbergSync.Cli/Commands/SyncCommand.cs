using System.CommandLine;
using System.CommandLine.Invocation;
using Microsoft.Extensions.DependencyInjection;
using GutenbergSync.Core.Configuration;
using GutenbergSync.Core.Sync;
using Serilog;
using Spectre.Console;

namespace GutenbergSync.Cli.Commands;

/// <summary>
/// Command for synchronizing Project Gutenberg archive
/// </summary>
public sealed class SyncCommand
{
    public static Command Create(IServiceProvider serviceProvider)
    {
        var command = new Command("sync", "Synchronize Project Gutenberg archive");

        var targetDirOption = new Option<string>(
            aliases: new[] { "--target-dir", "-t" },
            description: "Target directory for archive storage")
        {
            IsRequired = false
        };

        var presetOption = new Option<string>(
            aliases: new[] { "--preset", "-p" },
            description: "Content preset (text-only, text-epub, all-text, full)")
        {
            IsRequired = false
        };

        var metadataOnlyOption = new Option<bool>(
            "--metadata-only",
            description: "Sync only RDF metadata files (metadata-first strategy)")
        {
            IsRequired = false
        };

        var verifyOption = new Option<bool>(
            "--verify",
            description: "Verify file integrity after sync")
        {
            IsRequired = false
        };

        var dryRunOption = new Option<bool>(
            "--dry-run",
            description: "Preview sync without downloading files")
        {
            IsRequired = false
        };

        var autoRetryOption = new Option<bool>(
            "--auto-retry",
            description: "Automatically retry sync on failure (infinite retries)")
        {
            IsRequired = false
        };

        var maxRetriesOption = new Option<int?>(
            "--max-retries",
            description: "Maximum number of retry attempts (requires --auto-retry)")
        {
            IsRequired = false
        };

        var retryDelayOption = new Option<int>(
            "--retry-delay",
            description: "Seconds to wait before retry (default: 5)",
            getDefaultValue: () => 5)
        {
            IsRequired = false
        };

        var timeoutOption = new Option<int?>(
            "--timeout",
            description: "Timeout in seconds (0 = no timeout, default: 0 for long syncs)")
        {
            IsRequired = false
        };

        command.AddOption(targetDirOption);
        command.AddOption(presetOption);
        command.AddOption(metadataOnlyOption);
        command.AddOption(verifyOption);
        command.AddOption(dryRunOption);
        command.AddOption(autoRetryOption);
        command.AddOption(maxRetriesOption);
        command.AddOption(retryDelayOption);
        command.AddOption(timeoutOption);

        command.SetHandler(async (InvocationContext ctx) =>
        {
            var targetDir = ctx.ParseResult.GetValueForOption(targetDirOption);
            var preset = ctx.ParseResult.GetValueForOption(presetOption);
            var metadataOnly = ctx.ParseResult.GetValueForOption(metadataOnlyOption);
            var verify = ctx.ParseResult.GetValueForOption(verifyOption);
            var dryRun = ctx.ParseResult.GetValueForOption(dryRunOption);
            var autoRetry = ctx.ParseResult.GetValueForOption(autoRetryOption);
            var maxRetries = ctx.ParseResult.GetValueForOption(maxRetriesOption);
            var retryDelay = ctx.ParseResult.GetValueForOption(retryDelayOption);
            var timeout = ctx.ParseResult.GetValueForOption(timeoutOption);

            var logger = serviceProvider.GetRequiredService<ILogger>();
            var configLoader = serviceProvider.GetRequiredService<IConfigurationLoader>();
            var rsyncService = serviceProvider.GetRequiredService<IRsyncService>();

            try
            {
                var config = await configLoader.LoadAsync();
                
                // Override with command-line options
                if (!string.IsNullOrWhiteSpace(targetDir))
                {
                    config = config with
                    {
                        Sync = config.Sync with { TargetDirectory = targetDir }
                    };
                }

                var orchestrator = serviceProvider.GetRequiredService<ISyncOrchestrator>();

                var options = new SyncOrchestrationOptions
                {
                    TargetDirectory = config.Sync.TargetDirectory,
                    Preset = preset ?? config.Sync.Preset,
                    MetadataOnly = metadataOnly,
                    VerifyAfterSync = verify,
                    DryRun = dryRun,
                    TimeoutSeconds = timeout ?? 0 // Default to no timeout for long syncs
                };

                logger.Information("Starting sync operation...");
                AnsiConsole.MarkupLine("[green]Starting sync operation...[/]");
                
                if (autoRetry)
                {
                    var maxRetriesText = maxRetries.HasValue ? maxRetries.Value.ToString() : "∞";
                    AnsiConsole.MarkupLine($"[yellow]Auto-retry enabled: {maxRetriesText} max retries, {retryDelay}s delay[/]");
                }
                
                AnsiConsole.WriteLine(); // Line break before progress bars

                // Retry loop
                var retryCount = 0;
                var maxRetriesValue = autoRetry ? (maxRetries ?? int.MaxValue) : 0;
                SyncOrchestrationResult? result = null;

                while (true)
                {
                    if (retryCount > 0 && autoRetry)
                    {
                        if (retryCount > maxRetriesValue)
                        {
                            logger.Warning("Maximum retries ({MaxRetries}) reached", maxRetriesValue == int.MaxValue ? "∞" : maxRetriesValue.ToString());
                            AnsiConsole.MarkupLine($"[red]Maximum retries reached. Exiting.[/]");
                            break;
                        }
                        
                        logger.Information("Retry attempt {RetryCount}/{MaxRetries} (waiting {Delay}s...)", 
                            retryCount, maxRetriesValue == int.MaxValue ? "∞" : maxRetriesValue.ToString(), retryDelay);
                        AnsiConsole.MarkupLine($"[yellow]Retry attempt {retryCount}/{maxRetriesValue} (waiting {retryDelay}s...)[/]");
                        await Task.Delay(TimeSpan.FromSeconds(retryDelay));
                        AnsiConsole.MarkupLine("[cyan]Resuming sync...[/]");
                    }

                    // Track current file for display above progress bars
                    string? currentMetadataFile = null;
                    string? currentContentFile = null;

                    // Use Spectre.Console Progress for live updates
                    result = await AnsiConsole.Progress()
                    .AutoRefresh(true)
                    .AutoClear(false)
                    .HideCompleted(false)
                    .Columns(new ProgressColumn[]
                    {
                        new TaskDescriptionColumn(),
                        new ProgressBarColumn(),
                        new PercentageColumn(),
                        new SpinnerColumn()
                    })
                    .StartAsync(async ctx =>
                    {
                        var metadataTask = ctx.AddTask("[cyan]Metadata:[/] Syncing RDF files...", maxValue: 100);
                        var contentTask = ctx.AddTask("[cyan]Content:[/] Waiting...", maxValue: 100);
                        contentTask.IsIndeterminate = true; // Hide until content sync starts

                        var progress = new Progress<SyncOrchestrationProgress>(p =>
                        {
                            // Update progress on the current context (Spectre.Console handles thread safety)
                            if (p.Phase == "Metadata")
                            {
                                // Display file name on separate line if available
                                if (!string.IsNullOrWhiteSpace(p.CurrentFile) && p.CurrentFile != currentMetadataFile)
                                {
                                    currentMetadataFile = p.CurrentFile;
                                    AnsiConsole.MarkupLine($"[dim]  → {Markup.Escape(p.CurrentFile)}[/]");
                                }
                                
                                var message = p.Message;
                                // Keep description short to avoid pushing bars around
                                var shortMessage = message.Length > 50 ? message.Substring(0, 47) + "..." : message;
                                metadataTask.Description = $"[cyan]Metadata:[/] {Markup.Escape(shortMessage)}";
                                
                                // Always update the value if we have a percentage
                                if (p.ProgressPercent.HasValue)
                                {
                                    metadataTask.IsIndeterminate = false;
                                    metadataTask.Value = Math.Min(100, Math.Max(0, p.ProgressPercent.Value));
                                }
                                else
                                {
                                    // Show indeterminate progress if no percentage available
                                    metadataTask.IsIndeterminate = true;
                                }
                            }
                            else if (p.Phase == "Content")
                            {
                                // Display file name on separate line if available
                                if (!string.IsNullOrWhiteSpace(p.CurrentFile) && p.CurrentFile != currentContentFile)
                                {
                                    currentContentFile = p.CurrentFile;
                                    AnsiConsole.MarkupLine($"[dim]  → {Markup.Escape(p.CurrentFile)}[/]");
                                }
                                
                                contentTask.IsIndeterminate = false;
                                var message = p.Message;
                                // Keep description short to avoid pushing bars around
                                var shortMessage = message.Length > 50 ? message.Substring(0, 47) + "..." : message;
                                contentTask.Description = $"[cyan]Content:[/] {Markup.Escape(shortMessage)}";
                                
                                // Always update the value if we have a percentage
                                if (p.ProgressPercent.HasValue)
                                {
                                    contentTask.Value = Math.Min(100, Math.Max(0, p.ProgressPercent.Value));
                                }
                                else
                                {
                                    contentTask.IsIndeterminate = true;
                                }
                            }
                        });

                        return await orchestrator.SyncAsync(options, progress);
                    });

                    // If successful, break out of retry loop
                    if (result.Success)
                    {
                        break;
                    }

                    // Check if it was manually cancelled (don't retry on Ctrl+C)
                    var isCancelled = result.ErrorMessage?.Contains("cancelled") == true || 
                                     result.ErrorMessage?.Contains("resume") == true;
                    
                    if (isCancelled)
                    {
                        logger.Information("Sync was manually cancelled - not retrying");
                        break;
                    }

                    // If auto-retry is not enabled, break after first failure
                    if (!autoRetry)
                    {
                        break;
                    }

                    retryCount++;
                    logger.Warning("Sync failed, will retry: {Error}", result.ErrorMessage);
                    
                    // Check if we've exceeded max retries
                    if (retryCount > maxRetriesValue)
                    {
                        break;
                    }
                }

                if (result == null)
                {
                    logger.Error("Sync operation failed: No result returned");
                    AnsiConsole.MarkupLine("[red]✗ Sync operation failed: No result returned[/]");
                    Environment.ExitCode = 1;
                    return;
                }

                if (result.Success)
                {
                    logger.Information("Sync operation completed successfully");
                    AnsiConsole.MarkupLine("[green]✓ Sync operation completed successfully[/]");
                    
                    if (result.MetadataSync != null)
                    {
                        AnsiConsole.MarkupLine($"[green]  Metadata:[/] {result.MetadataSync.RecordsAdded} records added from {result.MetadataSync.RdfFilesSynced} RDF files");
                    }
                    
                    if (result.ContentSync != null)
                    {
                        AnsiConsole.MarkupLine($"[green]  Content:[/] {result.ContentSync.FilesSynced} files, {result.ContentSync.BytesTransferred:N0} bytes");
                    }
                    
                    AnsiConsole.MarkupLine($"[dim]  Duration: {result.Duration.TotalMinutes:F1} minutes[/]");
                }
                else
                {
                    // Check if it was cancelled vs. actual error
                    var isCancelled = result.ErrorMessage?.Contains("cancelled") == true || 
                                     result.ErrorMessage?.Contains("resume") == true;
                    
                    if (isCancelled)
                    {
                        logger.Information("Sync operation was cancelled: {Error}", result.ErrorMessage);
                        AnsiConsole.MarkupLine($"[yellow]⚠ Sync was cancelled[/]");
                        AnsiConsole.MarkupLine($"[cyan]  {Markup.Escape(result.ErrorMessage ?? string.Empty)}[/]");
                        AnsiConsole.MarkupLine("[dim]  Partial files preserved. Run the same command again to resume.[/]");
                    }
                    else
                    {
                        logger.Error("Sync operation failed: {Error}", result.ErrorMessage);
                        AnsiConsole.MarkupLine($"[red]✗ Sync operation failed: {Markup.Escape(result.ErrorMessage ?? string.Empty)}[/]");
                        Environment.ExitCode = 1;
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Sync operation failed");
                Environment.ExitCode = 1;
            }
        });

        return command;
    }
}

