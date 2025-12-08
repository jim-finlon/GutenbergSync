using GutenbergSync.Core.Sync;
using Xunit;

namespace GutenbergSync.Tests.Sync;

/// <summary>
/// Tests for SyncPresets
/// </summary>
public sealed class SyncPresetsTests
{
    [Fact]
    public void GetPresetPatterns_TextOnly_ReturnsTextOnlyPatterns()
    {
        var patterns = SyncPresets.GetPresetPatterns("text-only");
        
        Assert.Contains("*.txt", patterns);
        Assert.Contains("*.zip", patterns);
    }

    [Fact]
    public void GetPresetPatterns_TextEpub_ReturnsTextEpubPatterns()
    {
        var patterns = SyncPresets.GetPresetPatterns("text-epub");
        
        Assert.Contains("*.txt", patterns);
        Assert.Contains("*.zip", patterns);
        Assert.Contains("*.epub", patterns);
    }

    [Fact]
    public void GetPresetPatterns_AllText_ReturnsAllTextPatterns()
    {
        var patterns = SyncPresets.GetPresetPatterns("all-text");
        
        Assert.Contains("*.txt", patterns);
        Assert.Contains("*.html", patterns);
        Assert.Contains("*.htm", patterns);
    }

    [Fact]
    public void GetPresetPatterns_Full_ReturnsEmptyArray()
    {
        var patterns = SyncPresets.GetPresetPatterns("full");
        
        Assert.Empty(patterns);
    }

    [Fact]
    public void GetPresetPatterns_UnknownPreset_ReturnsTextOnly()
    {
        var patterns = SyncPresets.GetPresetPatterns("unknown");
        
        Assert.Contains("*.txt", patterns);
        Assert.Contains("*.zip", patterns);
    }

    [Fact]
    public void GetPresetPatterns_Null_ReturnsFull()
    {
        var patterns = SyncPresets.GetPresetPatterns(null);
        
        Assert.Empty(patterns);
    }
}

