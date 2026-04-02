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

public sealed class GoogleAiAdapterTests
{
    [Fact]
    public void CanHandle_ReturnsTrueOnlyForGoogleAi()
    {
        var adapter = new GoogleAiAdapter();

        Assert.True(adapter.CanHandle("GoogleAI"));
        Assert.True(adapter.CanHandle("googleai"));
        Assert.False(adapter.CanHandle("OpenAI"));
        Assert.False(adapter.CanHandle("Ollama"));
    }

    [Fact]
    public void BuildProvider_ReturnsGoogleAiProvider()
    {
        var adapter = new GoogleAiAdapter();

        var provider = adapter.BuildProvider(new ProviderRuntimeRequest(
            "GoogleAI",
            " api-key ",
            null,
            "gemini-2.5-flash",
            false));

        Assert.NotNull(provider);
        Assert.IsType<GoogleAiProvider>(provider);
    }

    [Fact]
    public void DefaultAdapterList_ResolvesGoogleAiToDedicatedAdapter()
    {
        var service = new ProviderAdapterApplicationService(ProviderAdapterDefaults.CreateDefaultAdapters());

        Assert.IsType<GoogleAiAdapter>(service.Resolve("GoogleAI"));
    }

    [Fact]
    public async Task ExecuteAsync_UsesSharedRequestAndExecutionSemantics()
    {
        var adapter = new GoogleAiAdapter();
        var provider = new FakeProvider(new AiResponse
        {
            IsSuccess = true,
            Content = "<tool_call>{\"tool\":\"open_url\",\"url\":\"https://example.com\"}</tool_call>",
            TokensUsed = 9
        });

        var result = await adapter.ExecuteAsync(provider, new ProviderRequestContext
        {
            Messages =
            [
                new ProviderRequestMessage { Role = "user", Content = "Open example.com" }
            ],
            EnabledToolCategories = ["web"],
            CancellationToken = CancellationToken.None
        });

        Assert.True(result.IsSuccess);
        Assert.Equal(WorkflowIntentKind.ExecuteTool, result.Intent?.Kind);
        Assert.Equal("open_url", result.Intent?.Tool?.ToolName);
        Assert.Equal(["web"], provider.LastEnabledToolCategories);
        Assert.Single(provider.LastMessages);
        Assert.Equal("Open example.com", provider.LastMessages[0].Content);
    }

    private sealed class FakeProvider : IAiProvider
    {
        private readonly AiResponse _response;

        public FakeProvider(AiResponse response)
        {
            _response = response;
        }

        public string ProviderType => "GoogleAI";
        public string DisplayName => "Fake GoogleAI";
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
