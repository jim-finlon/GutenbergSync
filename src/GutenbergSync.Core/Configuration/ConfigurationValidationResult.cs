namespace GutenbergSync.Core.Configuration;

/// <summary>
/// Result of configuration validation
/// </summary>
public sealed record ConfigurationValidationResult
{
    /// <summary>
    /// Whether the configuration is valid
    /// </summary>
    public bool IsValid { get; init; }

    /// <summary>
    /// Validation errors
    /// </summary>
    public IReadOnlyList<ValidationError> Errors { get; init; } = [];

    /// <summary>
    /// Validation warnings
    /// </summary>
    public IReadOnlyList<ValidationWarning> Warnings { get; init; } = [];
}

/// <summary>
/// A configuration validation error
/// </summary>
public sealed record ValidationError
{
    /// <summary>
    /// Path to the configuration property (e.g., "sync.targetDirectory")
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    /// Error message
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// Suggested fix (optional)
    /// </summary>
    public string? SuggestedFix { get; init; }
}

/// <summary>
/// A configuration validation warning
/// </summary>
public sealed record ValidationWarning
{
    /// <summary>
    /// Path to the configuration property
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    /// Warning message
    /// </summary>
    public required string Message { get; init; }
}

