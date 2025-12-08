namespace GutenbergSync.Core.Configuration;

/// <summary>
/// Configuration for logging
/// </summary>
public sealed record LoggingConfiguration
{
    /// <summary>
    /// Log level (Trace, Debug, Information, Warning, Error, Critical)
    /// </summary>
    public string Level { get; init; } = "Information";

    /// <summary>
    /// Path to log file (null = no file logging)
    /// </summary>
    public string? FilePath { get; init; }

    /// <summary>
    /// Number of days to retain log files
    /// </summary>
    public int RetainDays { get; init; } = 30;
}

