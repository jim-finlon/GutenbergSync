using System.CommandLine;
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

        command.AddOption(targetDirOption);
        command.AddOption(presetOption);
        command.AddOption(metadataOnlyOption);
        command.AddOption(verifyOption);
        command.AddOption(dryRunOption);

        command.SetHandler(async (targetDir, preset, metadataOnly, verify, dryRun) =>
        {
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
                    DryRun = dryRun
                };

                logger.Information("Starting sync operation...");
                AnsiConsole.MarkupLine("[green]Starting sync operation...[/]");

                // Use Spectre.Console Progress for live updates
                var result = await AnsiConsole.Progress()
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
                        var contentTask = ctx.AddTask("[cyan]Content:[/] Syncing files...", maxValue: 100);
                        contentTask.IsIndeterminate = true; // Hide until content sync starts

                        var progress = new Progress<SyncOrchestrationProgress>(p =>
                        {
                            if (p.Phase == "Metadata")
                            {
                                metadataTask.Description = $"[cyan]Metadata:[/] {p.Message}";
                                if (p.ProgressPercent.HasValue)
                                {
                                    metadataTask.Value = p.ProgressPercent.Value;
                                }
                            }
                            else if (p.Phase == "Content")
                            {
                                contentTask.IsIndeterminate = false;
                                contentTask.Description = $"[cyan]Content:[/] {p.Message}";
                                if (p.ProgressPercent.HasValue)
                                {
                                    contentTask.Value = p.ProgressPercent.Value;
                                }
                            }
                        });

                        return await orchestrator.SyncAsync(options, progress);
                    });

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
                        AnsiConsole.MarkupLine($"[cyan]  {result.ErrorMessage}[/]");
                        AnsiConsole.MarkupLine("[dim]  Partial files preserved. Run the same command again to resume.[/]");
                    }
                    else
                    {
                        logger.Error("Sync operation failed: {Error}", result.ErrorMessage);
                        AnsiConsole.MarkupLine($"[red]✗ Sync operation failed: {result.ErrorMessage}[/]");
                        Environment.ExitCode = 1;
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Sync operation failed");
                Environment.ExitCode = 1;
            }
        }, targetDirOption, presetOption, metadataOnlyOption, verifyOption, dryRunOption);

        return command;
    }
}

