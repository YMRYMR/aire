using System;
using Aire.Providers;
using Xunit;

namespace Aire.Tests.Providers;

public class InceptionProviderTests
{
    [Fact]
    public void Initialize_WithoutApiKey_ThrowsArgumentException()
    {
        InceptionProvider provider = new InceptionProvider();
        ProviderConfig config = new ProviderConfig
        {
            ApiKey = "",
            Model = "mercury-latest"
        };
        Assert.Throws<ArgumentException>(delegate
        {
            provider.Initialize(config);
        });
    }

    [Fact]
    public void Initialize_WithApiKey_DoesNotThrow()
    {
        InceptionProvider provider = new InceptionProvider();
        ProviderConfig config = new ProviderConfig
        {
            ApiKey = "sk-test-key",
            Model = "mercury-latest",
            BaseUrl = "https://api.inceptionlabs.ai"
        };
        Exception ex = Record.Exception(delegate
        {
            provider.Initialize(config);
        });
        Assert.Null(ex);
    }

    [Fact]
    public void ProviderType_IsInception()
    {
        InceptionProvider inceptionProvider = new InceptionProvider();
        Assert.Equal("Inception", inceptionProvider.ProviderType);
    }

    [Fact]
    public void DisplayName_ContainsInception()
    {
        InceptionProvider inceptionProvider = new InceptionProvider();
        Assert.Contains("Inception", inceptionProvider.DisplayName, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SupportsImages_IsFalseByDefault()
    {
        InceptionProvider inceptionProvider = new InceptionProvider();
        Assert.False(inceptionProvider.SupportsImages);
    }
}
