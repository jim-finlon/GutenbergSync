using System.Collections.Frozen;

namespace GutenbergSync.Core.Metadata;

/// <summary>
/// Maps between language names and ISO 639-1 codes
/// </summary>
public sealed class LanguageMapper : ILanguageMapper
{
    private static readonly FrozenDictionary<string, string> NameToIso = new Dictionary<string, string>
    {
        // Major languages
        ["English"] = "en",
        ["French"] = "fr",
        ["German"] = "de",
        ["Spanish"] = "es",
        ["Italian"] = "it",
        ["Portuguese"] = "pt",
        ["Russian"] = "ru",
        ["Chinese"] = "zh",
        ["Japanese"] = "ja",
        ["Arabic"] = "ar",
        ["Dutch"] = "nl",
        ["Greek"] = "el",
        ["Latin"] = "la",
        ["Polish"] = "pl",
        ["Swedish"] = "sv",
        ["Norwegian"] = "no",
        ["Danish"] = "da",
        ["Finnish"] = "fi",
        ["Czech"] = "cs",
        ["Hungarian"] = "hu",
        ["Romanian"] = "ro",
        ["Turkish"] = "tr",
        ["Hebrew"] = "he",
        ["Hindi"] = "hi",
        ["Korean"] = "ko",
        ["Vietnamese"] = "vi",
        ["Thai"] = "th",
        ["Indonesian"] = "id",
        ["Persian"] = "fa",
        ["Urdu"] = "ur",
        ["Bengali"] = "bn",
        ["Tamil"] = "ta",
        ["Telugu"] = "te",
        ["Marathi"] = "mr",
        ["Gujarati"] = "gu",
        ["Kannada"] = "kn",
        ["Malayalam"] = "ml",
        ["Punjabi"] = "pa",
        ["Odia"] = "or",
        ["Assamese"] = "as",
        ["Nepali"] = "ne",
        ["Sinhala"] = "si",
        ["Burmese"] = "my",
        ["Khmer"] = "km",
        ["Lao"] = "lo",
        ["Georgian"] = "ka",
        ["Armenian"] = "hy",
        ["Azerbaijani"] = "az",
        ["Kazakh"] = "kk",
        ["Kyrgyz"] = "ky",
        ["Uzbek"] = "uz",
        ["Tajik"] = "tg",
        ["Mongolian"] = "mn",
        ["Tibetan"] = "bo",
        ["Malay"] = "ms",
        ["Tagalog"] = "tl",
        ["Swahili"] = "sw",
        ["Zulu"] = "zu",
        ["Afrikaans"] = "af",
        ["Esperanto"] = "eo",
        ["Yiddish"] = "yi",
        ["Welsh"] = "cy",
        ["Irish"] = "ga",
        ["Scottish Gaelic"] = "gd",
        ["Breton"] = "br",
        ["Basque"] = "eu",
        ["Catalan"] = "ca",
        ["Galician"] = "gl",
        ["Occitan"] = "oc",
        ["Romansh"] = "rm",
        ["Frisian"] = "fy",
        ["Icelandic"] = "is",
        ["Faroese"] = "fo",
        ["Maltese"] = "mt",
        ["Albanian"] = "sq",
        ["Macedonian"] = "mk",
        ["Bulgarian"] = "bg",
        ["Serbian"] = "sr",
        ["Croatian"] = "hr",
        ["Bosnian"] = "bs",
        ["Slovenian"] = "sl",
        ["Slovak"] = "sk",
        ["Belarusian"] = "be",
        ["Ukrainian"] = "uk",
        ["Lithuanian"] = "lt",
        ["Latvian"] = "lv",
        ["Estonian"] = "et",
        ["Moldovan"] = "mo",
        ["Macedonian"] = "mk",
        // Common variations
        ["en"] = "en",
        ["fr"] = "fr",
        ["de"] = "de",
        ["es"] = "es",
        ["it"] = "it",
        ["pt"] = "pt",
        ["ru"] = "ru",
        ["zh"] = "zh",
        ["ja"] = "ja",
        ["ar"] = "ar"
    }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    private static readonly FrozenDictionary<string, string> IsoToName = NameToIso
        .Where(kvp => kvp.Key.Length > 3) // Only language names (exclude ISO code entries like "en" = "en")
        .ToDictionary(kvp => kvp.Value, kvp => kvp.Key, StringComparer.OrdinalIgnoreCase)
        .ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc/>
    public string? GetIsoCode(string languageName)
    {
        if (string.IsNullOrWhiteSpace(languageName))
            return null;

        // Check if it's already an ISO code
        if (languageName.Length == 2 || languageName.Length == 3)
        {
            if (IsoToName.ContainsKey(languageName))
                return languageName.ToLowerInvariant();
        }

        // Try to find by name
        return NameToIso.TryGetValue(languageName, out var isoCode) ? isoCode : null;
    }

    /// <inheritdoc/>
    public string? GetLanguageName(string isoCode)
    {
        if (string.IsNullOrWhiteSpace(isoCode))
            return null;

        return IsoToName.TryGetValue(isoCode, out var name) ? name : null;
    }

    /// <inheritdoc/>
    public bool TryMap(string input, out string? isoCode, out string? languageName)
    {
        isoCode = null;
        languageName = null;

        if (string.IsNullOrWhiteSpace(input))
            return false;

        // Check if input is an ISO code
        if (input.Length == 2 || input.Length == 3)
        {
            if (IsoToName.TryGetValue(input, out var name))
            {
                isoCode = input.ToLowerInvariant();
                languageName = name;
                return true;
            }
        }

        // Check if input is a language name
        if (NameToIso.TryGetValue(input, out var code))
        {
            isoCode = code;
            languageName = input;
            return true;
        }

        return false;
    }
}

