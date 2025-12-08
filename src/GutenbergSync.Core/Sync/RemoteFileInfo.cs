namespace GutenbergSync.Core.Sync;

/// <summary>
/// Information about a remote file
/// </summary>
public sealed record RemoteFileInfo
{
    /// <summary>
    /// File path (relative to module root)
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    /// File size in bytes
    /// </summary>
    public long SizeBytes { get; init; }

    /// <summary>
    /// Last modified timestamp
    /// </summary>
    public DateTime? LastModified { get; init; }
}

