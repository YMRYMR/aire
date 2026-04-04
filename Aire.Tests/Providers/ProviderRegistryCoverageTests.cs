using Aire.Data;
using Aire.Providers;
using Xunit;

namespace Aire.Tests.Providers;

public class ProviderRegistryCoverageTests
{
    [Fact]
    public void GetMetadata_UnknownTypeThrows()
    {
        Assert.Throws<NotSupportedException>(() => ProviderRegistry.GetMetadata("Something-Else"));
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

    [Fact]
    public void GetMetadata_AndCreateProvider_SupportGoogleAiImage()
    {
        var metadata = ProviderRegistry.GetMetadata("GoogleAIImage");
        Assert.IsType<GoogleAiImageProvider>(metadata);

        var provider = ProviderRegistry.CreateProvider(new Provider
        {
            Id = 999,
            Name = "Gemini Images",
            Type = "GoogleAIImage",
            ApiKey = "test-key",
            Model = "gemini-2.5-flash-image",
            IsEnabled = true
        });

        Assert.IsType<GoogleAiImageProvider>(provider);
        Assert.Equal("GoogleAIImage", provider.ProviderType);
    }
}
