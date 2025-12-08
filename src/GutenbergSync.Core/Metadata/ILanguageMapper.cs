namespace GutenbergSync.Core.Metadata;

/// <summary>
/// Maps between language names and ISO 639-1 codes
/// </summary>
public interface ILanguageMapper
{
    /// <summary>
    /// Gets the ISO 639-1 code for a language name
    /// </summary>
    string? GetIsoCode(string languageName);

    /// <summary>
    /// Gets the language name for an ISO 639-1 code
    /// </summary>
    string? GetLanguageName(string isoCode);

    /// <summary>
    /// Attempts to map input (name or code) to both ISO code and language name
    /// </summary>
    /// <param name="input">Language name or ISO code</param>
    /// <param name="isoCode">Output ISO code if found</param>
    /// <param name="languageName">Output language name if found</param>
    /// <returns>True if mapping was successful</returns>
    bool TryMap(string input, out string? isoCode, out string? languageName);
}

