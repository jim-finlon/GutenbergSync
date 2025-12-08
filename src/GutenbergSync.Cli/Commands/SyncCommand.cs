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
            "--target-dir",
            description: "Target directory for archive storage")
        {
            IsRequired = false
        };

        var presetOption = new Option<string>(
            "--preset",
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

                var progress = new Progress<SyncOrchestrationProgress>(p =>
                {
                    if (p.ProgressPercent.HasValue)
                    {
                        AnsiConsole.MarkupLine($"[cyan]{p.Phase}:[/] {p.Message} ({p.ProgressPercent:F1}%)");
                    }
                    else
                    {
                        AnsiConsole.MarkupLine($"[cyan]{p.Phase}:[/] {p.Message}");
                    }
                });

                var result = await orchestrator.SyncAsync(options, progress);

                if (result.Success)
                {
                    logger.Information("Sync operation completed successfully");
                    AnsiConsole.MarkupLine("[green]✓ Sync operation completed successfully[/]");
                    
                    if (result.MetadataSync != null)
                    {
                        AnsiConsole.MarkupLine($"[green]  Metadata:[/] {result.MetadataSync.RecordsAdded} records added");
                    }
                    
                    if (result.ContentSync != null)
                    {
                        AnsiConsole.MarkupLine($"[green]  Content:[/] {result.ContentSync.FilesSynced} files, {result.ContentSync.BytesTransferred:N0} bytes");
                    }
                }
                else
                {
                    logger.Error("Sync operation failed: {Error}", result.ErrorMessage);
                    AnsiConsole.MarkupLine($"[red]✗ Sync operation failed: {result.ErrorMessage}[/]");
                    Environment.ExitCode = 1;
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

