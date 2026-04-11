using Aire.Services;
using Xunit;

namespace Aire.Tests.Services;

public sealed class TokenEstimatorRegistryTests
{
    [Fact]
    public void GetEstimator_OpenAI_ReturnsOpenAiTokenEstimator()
    {
        var registry = new TokenEstimatorRegistry();
        var estimator = registry.GetEstimator("OpenAI");
        Assert.IsType<OpenAiTokenEstimator>(estimator);
    }

    [Fact]
    public void GetEstimator_Anthropic_ReturnsAnthropicTokenEstimator()
    {
        var registry = new TokenEstimatorRegistry();
        var estimator = registry.GetEstimator("Anthropic");
        Assert.IsType<AnthropicTokenEstimator>(estimator);
    }

    [Fact]
    public void GetEstimator_GoogleAI_ReturnsGoogleTokenEstimator()
    {
        var registry = new TokenEstimatorRegistry();
        var estimator = registry.GetEstimator("GoogleAI");
        Assert.IsType<GoogleTokenEstimator>(estimator);
    }

    [Fact]
    public void GetEstimator_Groq_ReturnsOpenAiTokenEstimator()
    {
        var registry = new TokenEstimatorRegistry();
        var estimator = registry.GetEstimator("Groq");
        Assert.IsType<OpenAiTokenEstimator>(estimator);
    }

    [Fact]
    public void GetEstimator_UnknownProvider_ReturnsDefaultCharacterEstimator()
    {
        var registry = new TokenEstimatorRegistry();
        var estimator = registry.GetEstimator("UnknownProvider");
        Assert.IsType<CharacterTokenEstimator>(estimator);
    }

    [Fact]
    public void GetEstimator_NullProvider_ReturnsDefault()
    {
        var registry = new TokenEstimatorRegistry();
        var estimator = registry.GetEstimator(null);
        Assert.IsType<CharacterTokenEstimator>(estimator);
    }

    [Fact]
    public void GetEstimator_EmptyProvider_ReturnsDefault()
    {
        var registry = new TokenEstimatorRegistry();
        var estimator = registry.GetEstimator("");
        Assert.IsType<CharacterTokenEstimator>(estimator);
    }

    [Fact]
    public void GetEstimator_ProviderCaseInsensitive()
    {
        var registry = new TokenEstimatorRegistry();
        var estimator1 = registry.GetEstimator("openai");
        var estimator2 = registry.GetEstimator("OpenAI");
        Assert.IsType<OpenAiTokenEstimator>(estimator1);
        Assert.IsType<OpenAiTokenEstimator>(estimator2);
    }

    [Fact]
    public void CustomMappings_OverrideDefaults()
    {
        var customEstimator = new CharacterTokenEstimator();
        var mappings = new Dictionary<string, ITokenEstimator>
        {
            ["OpenAI"] = customEstimator
        };
        var registry = new TokenEstimatorRegistry(mappings);
        var estimator = registry.GetEstimator("OpenAI");
        Assert.Same(customEstimator, estimator);
    }

    [Fact]
    public void CustomDefaultEstimator_UsedWhenProviderNotFound()
    {
        var defaultEstimator = new OpenAiTokenEstimator();
        var mappings = new Dictionary<string, ITokenEstimator>();
        var registry = new TokenEstimatorRegistry(mappings, defaultEstimator);
        var estimator = registry.GetEstimator("Unknown");
        Assert.Same(defaultEstimator, estimator);
    }
}