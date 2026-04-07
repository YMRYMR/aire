using Aire.AppLayer.Providers;
using Aire.Services.Providers;
using Xunit;

namespace Aire.Tests.Services;

public sealed class ProviderAdapterDefaultsCoverageTests
{
    [Theory]
    [InlineData("Codex", typeof(CodexCliAdapter))]
    [InlineData("ClaudeCode", typeof(ClaudeCodeAdapter))]
    [InlineData("OpenAI", typeof(OpenAiCompatibleAdapter))]
    [InlineData("Groq", typeof(OpenAiCompatibleAdapter))]
    [InlineData("OpenRouter", typeof(OpenAiCompatibleAdapter))]
    [InlineData("DeepSeek", typeof(OpenAiCompatibleAdapter))]
    [InlineData("Inception", typeof(OpenAiCompatibleAdapter))]
    [InlineData("Zai", typeof(OpenAiCompatibleAdapter))]
    [InlineData("GoogleAI", typeof(GoogleAiAdapter))]
    [InlineData("Anthropic", typeof(AnthropicAdapter))]
    [InlineData("ClaudeWeb", typeof(ClaudeWebAdapter))]
    [InlineData("Ollama", typeof(OllamaAdapter))]
    public void CreateDefaultAdapters_ResolvesKnownProvidersToDedicatedAdapters(string providerType, System.Type expectedAdapterType)
    {
        var resolver = new ProviderAdapterApplicationService(ProviderAdapterDefaults.CreateDefaultAdapters());

        var adapter = resolver.Resolve(providerType);

        Assert.IsType(expectedAdapterType, adapter);
    }

    [Fact]
    public void CreateDefaultAdapters_LeavesUnknownProvidersOnLegacyFallback()
    {
        var resolver = new ProviderAdapterApplicationService(ProviderAdapterDefaults.CreateDefaultAdapters());

        var adapter = resolver.Resolve("SomeFutureProvider");

        Assert.IsType<LegacyProviderAdapter>(adapter);
    }
}
