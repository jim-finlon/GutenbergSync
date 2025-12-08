namespace GutenbergSync.Core.Configuration;

/// <summary>
/// Loads application configuration from files and environment variables
/// </summary>
public interface IConfigurationLoader
{
    /// <summary>
    /// Loads configuration from a JSON file
    /// </summary>
    Task<AppConfiguration> LoadFromFileAsync(string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads configuration from a JSON file and applies environment variable overrides
    /// </summary>
    Task<AppConfiguration> LoadAsync(string? filePath = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a default configuration
    /// </summary>
    AppConfiguration CreateDefault();
}

