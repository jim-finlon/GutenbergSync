using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using GutenbergSync.Core.Catalog;
using GutenbergSync.Core.Infrastructure;
using GutenbergSync.Core.Sync;
using Serilog;

namespace GutenbergSync.Cli.Commands;

/// <summary>
/// Command for checking system health and status
/// </summary>
public sealed class HealthCommand
{
    public static Command Create(IServiceProvider serviceProvider)
    {
        var command = new Command("health", "Check system health and status");

        command.SetHandler(async () =>
        {
            var logger = serviceProvider.GetRequiredService<ILogger>();
            var rsyncDiscovery = serviceProvider.GetRequiredService<IRsyncDiscoveryService>();
            var catalogRepo = serviceProvider.GetRequiredService<ICatalogRepository>();

            try
            {
                logger.Information("Checking system health...");

                // Check rsync availability
                var rsyncResult = await rsyncDiscovery.DiscoverAsync();
                if (rsyncResult.IsAvailable)
                {
                    logger.Information("✓ rsync is available: {Path} ({Version})", 
                        rsyncResult.ExecutablePath, rsyncResult.Version);
                }
                else
                {
                    logger.Warning("✗ rsync is not available");
                    if (!string.IsNullOrWhiteSpace(rsyncResult.InstallationInstructions))
                    {
                        logger.Information(rsyncResult.InstallationInstructions);
                    }
                }

                // Check catalog
                try
                {
                    var stats = await catalogRepo.GetStatisticsAsync();
                    logger.Information("✓ Catalog database: {TotalBooks} books, {TotalAuthors} authors", 
                        stats.TotalBooks, stats.TotalAuthors);
                }
                catch
                {
                    logger.Warning("✗ Catalog database not initialized");
                }

                logger.Information("Health check completed");
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Health check failed");
                Environment.ExitCode = 1;
            }
        });

        return command;
    }
}

