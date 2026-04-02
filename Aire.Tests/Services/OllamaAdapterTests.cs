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

public sealed class OllamaAdapterTests
{
    [Fact]
    public void CanHandle_ReturnsTrueOnlyForOllama()
    {
        var adapter = new OllamaAdapter();

        Assert.True(adapter.CanHandle("Ollama"));
        Assert.True(adapter.CanHandle("ollama"));
        Assert.False(adapter.CanHandle("OpenAI"));
        Assert.False(adapter.CanHandle("GoogleAI"));
    }

    [Fact]
    public void BuildProvider_ReturnsOllamaProvider()
    {
        var adapter = new OllamaAdapter();

        var provider = adapter.BuildProvider(new ProviderRuntimeRequest(
            "Ollama",
            null,
            "http://localhost:11434",
            "qwen3:4b",
            false));

        Assert.NotNull(provider);
        Assert.IsType<OllamaProvider>(provider);
    }

    [Fact]
    public void DefaultAdapterList_ResolvesOllamaToDedicatedAdapter()
    {
        var service = new ProviderAdapterApplicationService(ProviderAdapterDefaults.CreateDefaultAdapters());

        Assert.IsType<OllamaAdapter>(service.Resolve("Ollama"));
    }

    [Fact]
    public async Task ExecuteAsync_UsesSharedRequestAndExecutionSemantics()
    {
        var adapter = new OllamaAdapter();
        var provider = new FakeProvider(new AiResponse
        {
            IsSuccess = true,
            Content = "<tool_call>{\"tool\":\"list_directory\",\"path\":\"C:\\\\dev\\\\aire\"}</tool_call>"
        });

        var result = await adapter.ExecuteAsync(provider, new ProviderRequestContext
        {
            Messages =
            [
                new ProviderRequestMessage { Role = "user", Content = "List the repo." }
            ],
            EnabledToolCategories = ["filesystem"],
            CancellationToken = CancellationToken.None
        });

        Assert.True(result.IsSuccess);
        Assert.Equal(WorkflowIntentKind.ExecuteTool, result.Intent?.Kind);
        Assert.Equal("list_directory", result.Intent?.Tool?.ToolName);
        Assert.Equal(["filesystem"], provider.LastEnabledToolCategories);
        Assert.Single(provider.LastMessages);
        Assert.Equal("List the repo.", provider.LastMessages[0].Content);
    }

    private sealed class FakeProvider : IAiProvider
    {
        private readonly AiResponse _response;

        public FakeProvider(AiResponse response)
        {
            _response = response;
        }

        public string ProviderType => "Ollama";
        public string DisplayName => "Fake Ollama";
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
