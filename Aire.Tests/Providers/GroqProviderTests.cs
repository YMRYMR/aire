using System.Threading;
using System.Threading.Tasks;
using Aire.Providers;
using Xunit;

namespace Aire.Tests.Providers;

public class GroqProviderTests
{
    [Fact]
    public void ProviderType_IsGroq()
    {
        GroqProvider groqProvider = new GroqProvider();
        Assert.Equal("Groq", groqProvider.ProviderType);
    }

    [Fact]
    public void DisplayName_IsGroq()
    {
        GroqProvider groqProvider = new GroqProvider();
        Assert.Equal("Groq", groqProvider.DisplayName);
    }

    [Fact]
    public void FieldHints_ShowBaseUrl_IsFalse()
    {
        GroqProvider groqProvider = new GroqProvider();
        Assert.False(groqProvider.FieldHints.ShowBaseUrl);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(new object[] { "" })]
    [InlineData(new object[] { " " })]
    public async Task FetchLiveModelsAsync_EmptyApiKey_ReturnsNull(string? apiKey)
    {
        GroqProvider provider = new GroqProvider();
        Assert.Null(await provider.FetchLiveModelsAsync(apiKey, null, CancellationToken.None));
    }
}
