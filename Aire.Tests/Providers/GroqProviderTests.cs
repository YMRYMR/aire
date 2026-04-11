using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Aire.Providers;
using Aire.Tests.Infrastructure;
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

    [Fact]
    public async Task FetchLiveModelsAsync_MapsAllModelsAsFreeToolCapableEntries()
    {
        using var server = new SimpleJsonServer((method, path) =>
            method == "GET" && path == "/v1/models"
                ? SimpleJsonServer.Json(200, """{"data":[{"id":"llama-3.3-70b"},{"id":"mixtral-8x7b"}]}""")
                : SimpleJsonServer.Json(404, """{"error":"missing"}"""));

        GroqProvider provider = new GroqProvider();

        var models = await provider.FetchLiveModelsAsync("groq-key", server.BaseUrl, CancellationToken.None);

        Assert.NotNull(models);
        Assert.Equal(new[] { "llama-3.3-70b", "mixtral-8x7b" }, models!.Select(m => m.Id).ToArray());
        Assert.All(models, m => Assert.Contains("tools", m.Capabilities));
        Assert.All(models, m => Assert.Contains("free", m.DisplayName, StringComparison.OrdinalIgnoreCase));
    }

}
