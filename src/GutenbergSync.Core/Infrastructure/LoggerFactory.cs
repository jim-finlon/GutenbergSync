using Serilog;
using Serilog.Events;
using GutenbergSync.Core.Configuration;

namespace GutenbergSync.Core.Infrastructure;

/// <summary>
/// Factory for creating configured Serilog loggers
/// </summary>
public sealed class LoggerFactory : ILoggerFactory
{
    /// <inheritdoc/>
    public ILogger CreateLogger(LoggingConfiguration config)
    {
        var logLevel = ParseLogLevel(config.Level);
        
        var loggerConfig = new LoggerConfiguration()
            .MinimumLevel.Is(logLevel)
            .Enrich.FromLogContext()
            .Enrich.WithMachineName()
            .Enrich.WithThreadId()
            .WriteTo.Console(
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}");

        // Add file sink if configured
        if (!string.IsNullOrWhiteSpace(config.FilePath))
        {
            loggerConfig.WriteTo.File(
                path: config.FilePath,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: config.RetainDays,
                outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u3}] {Message:lj}{NewLine}{Exception}",
                shared: true);
        }

        return loggerConfig.CreateLogger();
    }

    private static LogEventLevel ParseLogLevel(string level) => level.ToUpperInvariant() switch
    {
        "TRACE" => LogEventLevel.Verbose,
        "DEBUG" => LogEventLevel.Debug,
        "INFORMATION" or "INFO" => LogEventLevel.Information,
        "WARNING" or "WARN" => LogEventLevel.Warning,
        "ERROR" => LogEventLevel.Error,
        "CRITICAL" or "FATAL" => LogEventLevel.Fatal,
        _ => LogEventLevel.Information
    };
}

