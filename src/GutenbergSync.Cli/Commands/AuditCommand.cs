using System.CommandLine;
using System.IO;
using Microsoft.Extensions.DependencyInjection;
using GutenbergSync.Core.Sync;
using Serilog;
using Spectre.Console;

namespace GutenbergSync.Cli.Commands;

/// <summary>
/// Command for auditing and verifying file integrity
/// </summary>
public sealed class AuditCommand
{
    public static Command Create(IServiceProvider serviceProvider)
    {
        var command = new Command("audit", "Audit and verify file integrity");

        var scanCommand = new Command("scan", "Scan directory for missing or corrupt files");
        var directoryOption = new Option<string>(
            "--directory",
            description: "Directory to scan")
        {
            IsRequired = true
        };

        scanCommand.AddOption(directoryOption);
        scanCommand.SetHandler(async (directory) =>
        {
            var logger = serviceProvider.GetRequiredService<ILogger>();
            var auditService = serviceProvider.GetRequiredService<IAuditService>();

            try
            {
                AnsiConsole.MarkupLine($"[cyan]Scanning directory:[/] {directory}");

                var progress = new Progress<AuditProgress>(p =>
                {
                    if (p.CurrentFile != null)
                    {
                        AnsiConsole.MarkupLine($"[dim]Verifying:[/] {Path.GetFileName(p.CurrentFile)} ({p.ProgressPercent:F1}%)");
                    }
                });

                var result = await auditService.ScanDirectoryAsync(directory, progress);

                AnsiConsole.MarkupLine($"[green]Scan completed:[/]");
                AnsiConsole.MarkupLine($"  Total files: {result.TotalFiles}");
                AnsiConsole.MarkupLine($"[green]  Valid:[/] {result.ValidFiles}");
                AnsiConsole.MarkupLine($"[red]  Missing:[/] {result.MissingFiles}");
                AnsiConsole.MarkupLine($"[red]  Corrupt:[/] {result.CorruptFiles}");
                AnsiConsole.MarkupLine($"[yellow]  Size mismatches:[/] {result.SizeMismatchFiles}");
                AnsiConsole.MarkupLine($"[yellow]  Checksum mismatches:[/] {result.ChecksumMismatchFiles}");
                AnsiConsole.MarkupLine($"  Duration: {result.Duration.TotalSeconds:F1}s");
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Audit scan failed");
                AnsiConsole.MarkupLine($"[red]✗ Audit scan failed: {ex.Message}[/]");
                Environment.ExitCode = 1;
            }
        }, directoryOption);

        var verifyCommand = new Command("verify", "Verify files against catalog");
        verifyCommand.SetHandler(async () =>
        {
            var logger = serviceProvider.GetRequiredService<ILogger>();
            var auditService = serviceProvider.GetRequiredService<IAuditService>();

            try
            {
                AnsiConsole.MarkupLine("[cyan]Verifying catalog files...[/]");

                var progress = new Progress<AuditProgress>(p =>
                {
                    if (p.CurrentFile != null)
                    {
                        AnsiConsole.MarkupLine($"[dim]Verifying:[/] {p.CurrentFile} ({p.ProgressPercent:F1}%)");
                    }
                });

                var result = await auditService.VerifyCatalogAsync(progress);

                AnsiConsole.MarkupLine($"[green]Verification completed:[/]");
                AnsiConsole.MarkupLine($"  Total books: {result.TotalBooks}");
                AnsiConsole.MarkupLine($"[green]  Verified:[/] {result.VerifiedBooks}");
                AnsiConsole.MarkupLine($"[red]  Missing:[/] {result.MissingBooks}");
                AnsiConsole.MarkupLine($"[red]  Corrupt:[/] {result.CorruptBooks}");
                AnsiConsole.MarkupLine($"  Duration: {result.Duration.TotalSeconds:F1}s");
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Catalog verification failed");
                AnsiConsole.MarkupLine($"[red]✗ Catalog verification failed: {ex.Message}[/]");
                Environment.ExitCode = 1;
            }
        });

        command.AddCommand(scanCommand);
        command.AddCommand(verifyCommand);

        return command;
    }
}

