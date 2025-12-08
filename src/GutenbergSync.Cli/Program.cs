using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using GutenbergSync.Cli.Commands;
using GutenbergSync.Core.Configuration;
using GutenbergSync.Core.Infrastructure;
using Serilog;

namespace GutenbergSync.Cli;

/// <summary>
/// GutenbergSync CLI - Main entry point
/// </summary>
internal class Program
{
    /// <summary>
    /// Main entry point for the application
    /// </summary>
    public static async Task<int> Main(string[] args)
    {
        // Build service provider
        var services = new ServiceCollection();
        services.AddGutenbergSyncCore();

        // Configure logging
        var loggerFactory = services.BuildServiceProvider().GetRequiredService<ILoggerFactory>();
        var configLoader = services.BuildServiceProvider().GetRequiredService<IConfigurationLoader>();
        
        try
        {
            var config = await configLoader.LoadAsync();
            var logger = loggerFactory.CreateLogger(config.Logging);
            Log.Logger = logger;

            services.AddSingleton(Log.Logger);
        }
        catch
        {
            // Use default logger if config fails
            Log.Logger = new LoggerConfiguration()
                .WriteTo.Console()
                .CreateLogger();
            services.AddSingleton(Log.Logger);
        }

        var serviceProvider = services.BuildServiceProvider();

        // Build root command
        var rootCommand = new RootCommand("GutenbergSync - Project Gutenberg archive management tool");

        rootCommand.AddCommand(SyncCommand.Create(serviceProvider));
        rootCommand.AddCommand(CatalogCommand.Create(serviceProvider));
        rootCommand.AddCommand(ExtractCommand.Create(serviceProvider));
        rootCommand.AddCommand(ConfigCommand.Create(serviceProvider));
        rootCommand.AddCommand(HealthCommand.Create(serviceProvider));

        return await rootCommand.InvokeAsync(args);
    }
}

