namespace GutenbergSync.Core.Extraction;

/// <summary>
/// Markers used to identify Gutenberg headers and footers
/// </summary>
public static class GutenbergMarkers
{
    /// <summary>
    /// Start markers (text begins AFTER this line)
    /// </summary>
    public static readonly string[] StartMarkers =
    [
        "*** START OF THIS PROJECT GUTENBERG EBOOK",
        "*** START OF THE PROJECT GUTENBERG EBOOK",
        "***START OF THIS PROJECT GUTENBERG EBOOK",
        "*END*THE SMALL PRINT!",
        "*** START OF THIS PROJECT GUTENBERG"
    ];

    /// <summary>
    /// End markers (text ends BEFORE this line)
    /// </summary>
    public static readonly string[] EndMarkers =
    [
        "*** END OF THIS PROJECT GUTENBERG EBOOK",
        "*** END OF THE PROJECT GUTENBERG EBOOK",
        "***END OF THIS PROJECT GUTENBERG EBOOK",
        "End of Project Gutenberg",
        "End of the Project Gutenberg"
    ];
}

