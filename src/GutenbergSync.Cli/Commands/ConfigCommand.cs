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

        command.AddCommand(validateCommand);

        return command;
    }
}

