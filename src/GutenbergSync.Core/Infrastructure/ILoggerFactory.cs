using Serilog;

namespace GutenbergSync.Core.Infrastructure;

/// <summary>
/// Factory for creating configured Serilog loggers
/// </summary>
public interface ILoggerFactory
{
    /// <summary>
    /// Creates a configured logger based on logging configuration
    /// </summary>
    ILogger CreateLogger(Configuration.LoggingConfiguration config);
}

