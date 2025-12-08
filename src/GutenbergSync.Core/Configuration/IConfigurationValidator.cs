namespace GutenbergSync.Core.Configuration;

/// <summary>
/// Validates application configuration
/// </summary>
public interface IConfigurationValidator
{
    /// <summary>
    /// Validates the configuration and returns validation results
    /// </summary>
    Task<ConfigurationValidationResult> ValidateAsync(AppConfiguration config, CancellationToken cancellationToken = default);
}

