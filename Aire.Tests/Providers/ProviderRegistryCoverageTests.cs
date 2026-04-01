using Aire.Data;
using Aire.Providers;
using Xunit;

namespace Aire.Tests.Providers;

public class ProviderRegistryCoverageTests
{
    [Fact]
    public void GetMetadata_UnknownTypeFallsBackToOpenAiAndCachesByKey()
    {
        IProviderMetadata metadata = ProviderRegistry.GetMetadata("Something-Else");
        IProviderMetadata metadata2 = ProviderRegistry.GetMetadata("Something-Else");
        Assert.IsType<OpenAiProvider>(metadata);
        Assert.Same(metadata, metadata2);
    }

    [Fact]
    public void BuildProviderConfig_ClampsTimeoutAndPreservesModelDetails()
    {
        ProviderConfig providerConfig = ProviderRegistry.BuildProviderConfig(new Provider
        {
            Type = "OpenAI",
            Model = "missing-model-id",
            ApiKey = null,
            BaseUrl = "https://example.invalid",
            TimeoutMinutes = 0
        });
        ProviderConfig providerConfig2 = ProviderRegistry.BuildProviderConfig(new Provider
        {
            Type = "OpenAI",
            Model = "missing-model-id",
            ApiKey = "abc",
            TimeoutMinutes = 50000
        });
        Assert.Equal(string.Empty, providerConfig.ApiKey);
        Assert.Equal("https://example.invalid", providerConfig.BaseUrl);
        Assert.Equal(1, providerConfig.TimeoutMinutes);
        Assert.Null(providerConfig.ModelCapabilities);
        Assert.Equal(35791, providerConfig2.TimeoutMinutes);
        Assert.Null(providerConfig2.ModelCapabilities);
    }
}
