namespace GutenbergSync.Core.Sync;

/// <summary>
/// Content filtering presets for sync operations
/// </summary>
public static class SyncPresets
{
    /// <summary>
    /// Text only - smallest footprint (~15GB)
    /// Includes: .txt files, .zip containing .txt
    /// </summary>
    public static readonly string[] TextOnly = { "*.txt", "*.zip" };

    /// <summary>
    /// Text and EPUB - balanced (~50GB)
    /// Includes: .txt, .zip, .epub, .epub.noimages
    /// </summary>
    public static readonly string[] TextEpub = { "*.txt", "*.zip", "*.epub", "*.epub.noimages" };

    /// <summary>
    /// All text formats - comprehensive text (~40GB uncompressed)
    /// Includes: .txt, .zip, .html, .htm
    /// </summary>
    public static readonly string[] AllText = { "*.txt", "*.zip", "*.html", "*.htm" };

    /// <summary>
    /// Full archive - everything (~1TB with audio)
    /// Includes: All file types
    /// </summary>
    public static readonly string[] Full = Array.Empty<string>(); // Empty means no filtering

    /// <summary>
    /// Gets include patterns for a preset name
    /// </summary>
    public static string[] GetPresetPatterns(string? presetName)
    {
        if (string.IsNullOrWhiteSpace(presetName))
            return Full;

        return presetName.ToLowerInvariant() switch
        {
            "text-only" => TextOnly,
            "text-epub" => TextEpub,
            "all-text" => AllText,
            "full" => Full,
            _ => TextOnly // Default to text-only
        };
    }
}

