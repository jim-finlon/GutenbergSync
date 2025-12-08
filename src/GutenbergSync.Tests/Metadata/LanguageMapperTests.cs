using GutenbergSync.Core.Metadata;
using Xunit;

namespace GutenbergSync.Tests.Metadata;

/// <summary>
/// Tests for LanguageMapper
/// </summary>
public sealed class LanguageMapperTests
{
    private readonly LanguageMapper _mapper = new();

    [Fact]
    public void GetIsoCode_English_ReturnsEn()
    {
        var result = _mapper.GetIsoCode("English");
        Assert.Equal("en", result);
    }

    [Fact]
    public void GetIsoCode_French_ReturnsFr()
    {
        var result = _mapper.GetIsoCode("French");
        Assert.Equal("fr", result);
    }

    [Fact]
    public void GetIsoCode_UnknownLanguage_ReturnsNull()
    {
        var result = _mapper.GetIsoCode("UnknownLanguage");
        Assert.Null(result);
    }

    [Fact]
    public void GetLanguageName_En_ReturnsEnglish()
    {
        var result = _mapper.GetLanguageName("en");
        Assert.Equal("English", result);
    }

    [Fact]
    public void GetLanguageName_Fr_ReturnsFrench()
    {
        var result = _mapper.GetLanguageName("fr");
        Assert.Equal("French", result);
    }

    [Fact]
    public void GetLanguageName_UnknownCode_ReturnsNull()
    {
        var result = _mapper.GetLanguageName("xx");
        Assert.Null(result);
    }

    [Fact]
    public void TryMap_EnglishName_ReturnsBothNameAndCode()
    {
        var success = _mapper.TryMap("English", out var isoCode, out var languageName);
        
        Assert.True(success);
        Assert.Equal("en", isoCode);
        Assert.Equal("English", languageName);
    }

    [Fact]
    public void TryMap_IsoCode_ReturnsBothNameAndCode()
    {
        var success = _mapper.TryMap("en", out var isoCode, out var languageName);
        
        Assert.True(success);
        Assert.Equal("en", isoCode);
        Assert.Equal("English", languageName);
    }

    [Fact]
    public void TryMap_UnknownInput_ReturnsFalse()
    {
        var success = _mapper.TryMap("Unknown", out var isoCode, out var languageName);
        
        Assert.False(success);
        Assert.Null(isoCode);
        Assert.Null(languageName);
    }
}

