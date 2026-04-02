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

public sealed class ClaudeProviderAdapterTests
{
    [Fact]
    public void DefaultAdapterList_ResolvesClaudeProvidersToDedicatedAdapters()
    {
        var service = new ProviderAdapterApplicationService(ProviderAdapterDefaults.CreateDefaultAdapters());

        Assert.IsType<AnthropicAdapter>(service.Resolve("Anthropic"));
        Assert.IsType<ClaudeWebAdapter>(service.Resolve("ClaudeWeb"));
    }

    [Fact]
    public void AnthropicAdapter_BuildProvider_ReturnsClaudeAiProvider()
    {
        var adapter = new AnthropicAdapter();

        var provider = adapter.BuildProvider(new ProviderRuntimeRequest(
            "Anthropic",
            "key",
            null,
            "claude-sonnet-4-5",
            false));

        Assert.NotNull(provider);
        Assert.IsType<ClaudeAiProvider>(provider);
    }

    [Fact]
    public void ClaudeWebAdapter_BuildProvider_ReturnsNull_WhenSessionNotReady()
    {
        var adapter = new ClaudeWebAdapter();

        var provider = adapter.BuildProvider(new ProviderRuntimeRequest(
            "ClaudeWeb",
            null,
            null,
            "claude-sonnet-4-5",
            false));

        Assert.Null(provider);
    }

    [Fact]
    public async Task ClaudeWebAdapter_ExecuteAsync_UsesSharedRequestAndExecutionSemantics()
    {
        var adapter = new ClaudeWebAdapter();
        var provider = new FakeProvider("ClaudeWeb", new AiResponse
        {
            IsSuccess = true,
            Content = "<tool_call>{\"tool\":\"ask_followup_question\",\"question\":\"Which folder?\"}</tool_call>"
        });

        var result = await adapter.ExecuteAsync(provider, new ProviderRequestContext
        {
            Messages =
            [
                new ProviderRequestMessage { Role = "user", Content = "Ask a follow-up." }
            ],
            EnabledToolCategories = ["agent"],
            CancellationToken = CancellationToken.None
        });

        Assert.True(result.IsSuccess);
        Assert.Equal(WorkflowIntentKind.FollowUpQuestion, result.Intent?.Kind);
        Assert.Equal(["agent"], provider.LastEnabledToolCategories);
        Assert.Single(provider.LastMessages);
    }

    private sealed class FakeProvider : IAiProvider
    {
        private readonly AiResponse _response;

        public FakeProvider(string providerType, AiResponse response)
        {
            ProviderType = providerType;
            _response = response;
        }

        public string ProviderType { get; }
        public string DisplayName => ProviderType;
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
