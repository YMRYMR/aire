using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Aire.AppLayer.Abstractions;
using Aire.AppLayer.Providers;
using Aire.Domain.Providers;
using Aire.Providers;
using Aire.Services.Providers;
using Xunit;

namespace Aire.Tests.Services;

public sealed class OpenAiCompatibleAdapterTests
{
    [Theory]
    [InlineData("OpenAI")]
    [InlineData("Groq")]
    [InlineData("OpenRouter")]
    [InlineData("Mistral")]
    [InlineData("DeepSeek")]
    [InlineData("Inception")]
    [InlineData("Zai")]
    public void CanHandle_ReturnsTrue_ForSupportedProviderTypes(string providerType)
    {
        var adapter = new OpenAiCompatibleAdapter();

        Assert.True(adapter.CanHandle(providerType));
        Assert.True(adapter.CanHandle(providerType.ToLowerInvariant()));
    }

    [Theory]
    [InlineData("Codex")]
    [InlineData("GoogleAI")]
    [InlineData("Ollama")]
    [InlineData("Anthropic")]
    [InlineData("")]
    public void CanHandle_ReturnsFalse_ForOtherProviderTypes(string providerType)
    {
        var adapter = new OpenAiCompatibleAdapter();

        Assert.False(adapter.CanHandle(providerType));
    }

    [Theory]
    [InlineData("OpenAI", typeof(OpenAiProvider))]
    [InlineData("Groq", typeof(GroqProvider))]
    [InlineData("OpenRouter", typeof(OpenRouterProvider))]
    [InlineData("Mistral", typeof(MistralProvider))]
    [InlineData("DeepSeek", typeof(DeepSeekProvider))]
    [InlineData("Inception", typeof(InceptionProvider))]
    [InlineData("Zai", typeof(ZaiProvider))]
    public void BuildProvider_ReturnsExpectedRuntimeProvider(string providerType, System.Type expectedType)
    {
        var adapter = new OpenAiCompatibleAdapter();
        var baseUrl = string.Equals(providerType, "Zai", System.StringComparison.OrdinalIgnoreCase)
            ? "https://api.z.ai/api/paas/v4/"
            : " https://example.test ";
        var model = string.Equals(providerType, "Zai", System.StringComparison.OrdinalIgnoreCase)
            ? "glm-5"
            : "model-x";

        var provider = adapter.BuildProvider(new ProviderRuntimeRequest(
            providerType,
            " api-key ",
            baseUrl,
            model,
            false));

        Assert.NotNull(provider);
        Assert.IsType(expectedType, provider);
    }

    [Fact]
    public void DefaultAdapterList_ResolvesOpenAiFamilyToDedicatedAdapter()
    {
        var service = new ProviderAdapterApplicationService(ProviderAdapterDefaults.CreateDefaultAdapters());

        Assert.IsType<OpenAiCompatibleAdapter>(service.Resolve("OpenAI"));
        Assert.IsType<OpenAiCompatibleAdapter>(service.Resolve("Groq"));
        Assert.IsType<OpenAiCompatibleAdapter>(service.Resolve("Mistral"));
        Assert.IsType<OpenAiCompatibleAdapter>(service.Resolve("Zai"));
    }

    [Fact]
    public async Task ExecuteAsync_UsesSharedRequestAndExecutionSemantics()
    {
        var adapter = new OpenAiCompatibleAdapter();
        var provider = new FakeProvider(new AiResponse
        {
            IsSuccess = true,
            Content = "<tool_call>{\"tool\":\"search_files\",\"pattern\":\"*.cs\",\"directory\":\"C:\\\\dev\\\\aire\"}</tool_call>",
            TokensUsed = 7
        });

        var result = await adapter.ExecuteAsync(provider, new ProviderRequestContext
        {
            Messages =
            [
                new ProviderRequestMessage { Role = "user", Content = "Search the repo." }
            ],
            EnabledToolCategories = ["filesystem"],
            CancellationToken = CancellationToken.None
        });

        Assert.True(result.IsSuccess);
        Assert.Equal(WorkflowIntentKind.ExecuteTool, result.Intent?.Kind);
        Assert.Equal("search_files", result.Intent?.Tool?.ToolName);
        Assert.Equal(7, result.TokensUsed);
        Assert.Equal(["filesystem"], provider.LastEnabledToolCategories);
        Assert.Single(provider.LastMessages);
        Assert.Equal("Search the repo.", provider.LastMessages[0].Content);
    }

    private sealed class FakeProvider : IAiProvider
    {
        private readonly AiResponse _response;

        public FakeProvider(AiResponse response)
        {
            _response = response;
        }

        public string ProviderType => "OpenAI";
        public string DisplayName => "Fake OpenAI";
        public ProviderCapabilities Capabilities => ProviderCapabilities.TextChat | ProviderCapabilities.ToolCalling;
        public ToolCallMode ToolCallMode => ToolCallMode.TextBased;
        public ToolOutputFormat ToolOutputFormat => ToolOutputFormat.AireText;
        public IReadOnlyList<string>? LastEnabledToolCategories { get; private set; }
        public IReadOnlyList<ChatMessage> LastMessages { get; private set; } = [];

        public bool Has(ProviderCapabilities cap) => (Capabilities & cap) == cap;
        public void Initialize(ProviderConfig config) { }
        public Task<AiResponse> SendChatAsync(IEnumerable<ChatMessage> messages, CancellationToken cancellationToken = default)
        {
            LastMessages = messages is IReadOnlyList<ChatMessage> list ? list : [.. messages];
            return Task.FromResult(_response);
        }
        public void PrepareForCapabilityTesting() { }
        public void SetToolsEnabled(bool enabled) { }
        public void SetEnabledToolCategories(IEnumerable<string>? categories)
            => LastEnabledToolCategories = categories is null ? null : [.. categories];
        public async IAsyncEnumerable<string> StreamChatAsync(IEnumerable<ChatMessage> messages, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield break;
        }
        public Task<ProviderValidationResult> ValidateConfigurationAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(ProviderValidationResult.Ok());
        public Task<TokenUsage?> GetTokenUsageAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<TokenUsage?>(null);
    }
}
