using System;
using Aire.Services;
using Xunit;

namespace Aire.Tests.Services;

public class LocalizationServiceTests
{
    [Fact]
    public void LoadAll_DoesNotCrash_WhenDirectoryIsMissing()
    {
        Exception ex = Record.Exception(delegate
        {
            LocalizationService.LoadAll();
        });
        Assert.Null(ex);
    }

    [Fact]
    public void S_ReturnsFallbackOrKey_WhenKeyIsMissing()
    {
        string actual = LocalizationService.S("Missing.Key.Here");
        Assert.Equal("Missing.Key.Here", actual);
        string actual2 = LocalizationService.S("Missing.Key.Here", "FallbackValue");
        Assert.Equal("FallbackValue", actual2);
    }

    [Fact]
    public void SetLanguage_DoesNotCrash_OnUnknownCode()
    {
        Exception ex = Record.Exception(delegate
        {
            LocalizationService.SetLanguage("unknown_fake_lang_code");
        });
        Assert.Null(ex);
    }
}
