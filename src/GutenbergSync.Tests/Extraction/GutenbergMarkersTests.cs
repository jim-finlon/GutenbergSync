using GutenbergSync.Core.Extraction;
using Xunit;

namespace GutenbergSync.Tests.Extraction;

/// <summary>
/// Tests for GutenbergMarkers
/// </summary>
public sealed class GutenbergMarkersTests
{
    [Fact]
    public void StartMarkers_ContainsExpectedMarkers()
    {
        Assert.Contains("*** START OF THIS PROJECT GUTENBERG EBOOK", GutenbergMarkers.StartMarkers);
        Assert.Contains("*** START OF THE PROJECT GUTENBERG EBOOK", GutenbergMarkers.StartMarkers);
    }

    [Fact]
    public void EndMarkers_ContainsExpectedMarkers()
    {
        Assert.Contains("*** END OF THIS PROJECT GUTENBERG EBOOK", GutenbergMarkers.EndMarkers);
        Assert.Contains("*** END OF THE PROJECT GUTENBERG EBOOK", GutenbergMarkers.EndMarkers);
    }

    [Fact]
    public void StartMarkers_IsNotEmpty()
    {
        Assert.NotEmpty(GutenbergMarkers.StartMarkers);
    }

    [Fact]
    public void EndMarkers_IsNotEmpty()
    {
        Assert.NotEmpty(GutenbergMarkers.EndMarkers);
    }
}

