using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using GutenbergSync.Core.Configuration;
using Serilog;

namespace GutenbergSync.Cli.Commands;

/// <summary>
/// Command for configuration management
/// </summary>
public sealed class ConfigCommand
{
    public static Command Create(IServiceProvider serviceProvider)
    {
        var command = new Command("config", "Manage configuration");

        var initCommand = new Command("init", "Initialize a default configuration file");
        var initPathOption = new Option<string>(
            "--path",
            description: "Path where to create the configuration file",
            getDefaultValue: () => "config.json");

        initCommand.AddOption(initPathOption);
        initCommand.SetHandler(async (path) =>
        {
            var logger = serviceProvider.GetRequiredService<ILogger>();
            var configLoader = serviceProvider.GetRequiredService<IConfigurationLoader>();

            try
            {
                if (File.Exists(path))
                {
                    logger.Warning("Configuration file already exists: {Path}", path);
                    logger.Information("Use --path to specify a different location");
                    return;
                }

                var defaultConfig = configLoader.CreateDefault();
                var json = System.Text.Json.JsonSerializer.Serialize(defaultConfig, new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true
                });

                await File.WriteAllTextAsync(path, json);
                logger.Information("Created default configuration file: {Path}", path);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Failed to create configuration file");
                Environment.ExitCode = 1;
            }
        }, initPathOption);

        var validateCommand = new Command("validate", "Validate configuration file");
        var configFileOption = new Option<string>(
            "--config",
            description: "Path to configuration file");

        validateCommand.AddOption(configFileOption);

        validateCommand.SetHandler(async (configFile) =>
        {
            var logger = serviceProvider.GetRequiredService<ILogger>();
            var configLoader = serviceProvider.GetRequiredService<IConfigurationLoader>();
            var validator = serviceProvider.GetRequiredService<IConfigurationValidator>();

            try
            {
                var config = await configLoader.LoadAsync(configFile);
                var result = await validator.ValidateAsync(config);

                if (result.IsValid)
                {
                    logger.Information("Configuration is valid");
                }
                else
                {
                    logger.Error("Configuration validation failed:");
                    foreach (var error in result.Errors)
                    {
                        logger.Error("  {Path}: {Message}", error.Path, error.Message);
                        if (!string.IsNullOrWhiteSpace(error.SuggestedFix))
                        {
                            logger.Information("    Suggested fix: {Fix}", error.SuggestedFix);
                        }
                    }
                    Environment.ExitCode = 1;
                }

                if (result.Warnings.Count > 0)
                {
                    logger.Warning("Configuration warnings:");
                    foreach (var warning in result.Warnings)
                    {
                        logger.Warning("  {Path}: {Message}", warning.Path, warning.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Configuration validation failed");
                Environment.ExitCode = 1;
            }
        }, configFileOption);

        command.AddCommand(initCommand);
        command.AddCommand(validateCommand);

        return command;
    }
}

