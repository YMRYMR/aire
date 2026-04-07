using Aire.Providers;
using Xunit;

namespace Aire.Tests.Providers;

public sealed class ProviderCatalogTests
{
    [Theory]
    [InlineData("chatgpt", "OpenAI")]
    [InlineData("claude.ai", "ClaudeWeb")]
    [InlineData("claude code", "ClaudeCode")]
    [InlineData("Google AI Images", "GoogleAIImage")]
    [InlineData("z.ai", "Zai")]
    public void NormalizeType_MapsKnownAliases(string input, string expected)
    {
        Assert.Equal(expected, ProviderIdentityCatalog.NormalizeType(input));
        Assert.Equal(expected, ProviderCatalog.NormalizeType(input));
    }

    [Fact]
    public void IdentityAndRuntimeCatalogs_StayAligned_ForRepresentativeProviders()
    {
        var identity = ProviderIdentityCatalog.GetDescriptor("Zai");
        var catalog = ProviderCatalog.GetDescriptor("Zai");

        Assert.Equal(identity.Type, catalog.Type);
        Assert.Equal(identity.DisplayName, catalog.DisplayName);
        Assert.Equal(identity.DefaultName, catalog.DefaultName);
        Assert.Equal(identity.ApiKeyUrl, catalog.ApiKeyUrl);
        Assert.Equal(identity.SignUpUrl, catalog.SignUpUrl);
        Assert.Equal(identity.RequiresApiKey, catalog.RequiresApiKey);
        Assert.Equal(identity.SupportsSessionCredential, catalog.SupportsSessionCredential);
        Assert.Equal("Zai", ProviderCatalog.CreateRuntimeProvider("Zai").ProviderType);
        Assert.Equal("Zai", ProviderCatalog.CreateMetadataProvider("Zai").ProviderType);

        var claudeCodeIdentity = ProviderIdentityCatalog.GetDescriptor("ClaudeCode");
        var claudeCodeCatalog = ProviderCatalog.GetDescriptor("ClaudeCode");
        Assert.Equal(claudeCodeIdentity.Type, claudeCodeCatalog.Type);
        Assert.Equal("ClaudeCode", ProviderCatalog.CreateRuntimeProvider("ClaudeCode").ProviderType);
        Assert.Equal("ClaudeCode", ProviderCatalog.CreateMetadataProvider("ClaudeCode").ProviderType);
    }

    [Fact]
    public void TryGetDescriptor_ReturnsFalse_ForUnknownProvider()
    {
        Assert.False(ProviderIdentityCatalog.TryGetDescriptor("unknown-provider", out _));
        Assert.False(ProviderCatalog.TryGetDescriptor("unknown-provider", out _));
    }
}
