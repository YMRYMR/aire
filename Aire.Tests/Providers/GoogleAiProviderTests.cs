using System.Threading;
using System.Threading.Tasks;
using Aire.Providers;
using Xunit;

namespace Aire.Tests.Providers;

public class GoogleAiProviderTests
{
    [Fact]
    public void ProviderType_IsGoogleAI()
    {
        GoogleAiProvider googleAiProvider = new GoogleAiProvider();
        Assert.Equal("GoogleAI", googleAiProvider.ProviderType);
    }

    [Fact]
    public void DisplayName_IsGoogleAIGemini()
    {
        GoogleAiProvider googleAiProvider = new GoogleAiProvider();
        Assert.Equal("Google AI (Gemini)", googleAiProvider.DisplayName);
    }

    [Fact]
    public void FieldHints_ShowBaseUrl_IsFalse()
    {
        GoogleAiProvider googleAiProvider = new GoogleAiProvider();
        Assert.False(googleAiProvider.FieldHints.ShowBaseUrl);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(new object[] { "" })]
    [InlineData(new object[] { " " })]
    public async Task ValidateConfigurationAsync_EmptyApiKey_ReturnsFalse(string? apiKey)
    {
        GoogleAiProvider provider = new GoogleAiProvider();
        provider.Initialize(new ProviderConfig
        {
            ApiKey = apiKey,
            Model = "gemini-1.5-pro"
        });
        var validation = await provider.ValidateConfigurationAsync(CancellationToken.None);
        Assert.False(validation.IsValid);
        Assert.NotNull(validation.Error);
        Assert.NotEmpty(validation.Error);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(new object[] { "" })]
    [InlineData(new object[] { " " })]
    public async Task FetchLiveModelsAsync_EmptyApiKey_ReturnsNull(string? apiKey)
    {
        GoogleAiProvider provider = new GoogleAiProvider();
        Assert.Null(await provider.FetchLiveModelsAsync(apiKey, null, CancellationToken.None));
    }
}
