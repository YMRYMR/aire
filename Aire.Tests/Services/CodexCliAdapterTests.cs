using System.Threading;
using System.Threading.Tasks;
using Aire.AppLayer.Abstractions;
using Aire.AppLayer.Providers;
using Aire.Domain.Providers;
using Aire.Providers;
using Aire.Services.Providers;
using Xunit;

namespace Aire.Tests.Services;

public sealed class CodexCliAdapterTests
{
    [Fact]
    public void CanHandle_IsTrueForCodex_IgnoringCase()
    {
        var adapter = new CodexCliAdapter();

        Assert.True(adapter.CanHandle("Codex"));
        Assert.True(adapter.CanHandle("codex"));
        Assert.False(adapter.CanHandle("OpenAI"));
    }

    [Fact]
    public void BuildProvider_ReturnsConfiguredCodexProvider()
    {
        var adapter = new CodexCliAdapter();

        var provider = adapter.BuildProvider(new ProviderRuntimeRequest(
            "Codex",
            null,
            " https://example.test ",
            "gpt-5.4-mini",
            false));

        var codex = Assert.IsType<CodexProvider>(provider);
        Assert.Equal("Codex", codex.ProviderType);
        Assert.Equal("gpt-5.4-mini", GetConfig(codex).Model);
        Assert.Equal("https://example.test", GetConfig(codex).BaseUrl);
    }

    [Fact]
    public void BuildProvider_ReturnsNull_ForOtherProviderTypes()
    {
        var adapter = new CodexCliAdapter();

        Assert.Null(adapter.BuildProvider(new ProviderRuntimeRequest("OpenAI", "k", null, "gpt-5.4-mini", false)));
    }

    [Fact]
    public void DefaultAdapterList_ResolvesCodexToDedicatedAdapter()
    {
        var service = new ProviderAdapterApplicationService(ProviderAdapterDefaults.CreateDefaultAdapters());

        var adapter = service.Resolve("Codex");

        Assert.IsType<CodexCliAdapter>(adapter);
    }

    [Fact]
    public async Task ValidateAsync_MapsLegacyResultIntoValidationOutcome()
    {
        var adapter = new CodexCliAdapter();
        var provider = new FakeCodexProvider(ProviderValidationResult.Fail("Invalid API key"));

        var outcome = await adapter.ValidateAsync(provider, CancellationToken.None);

        Assert.False(outcome.IsValid);
        Assert.Equal(ProviderValidationFailureKind.InvalidCredentials, outcome.FailureKind);
    }

    [Fact]
    public async Task ExecuteAsync_UsesSharedRequestAndExecutionSemantics()
    {
        var adapter = new CodexCliAdapter();
        var provider = new FakeCodexProvider(
            ProviderValidationResult.Ok(),
            new AiResponse
            {
                IsSuccess = true,
                Content = "<tool_call>{\"tool\":\"list_directory\",\"path\":\"C:\\\\dev\\\\aire\"}</tool_call>",
                TokensUsed = 42
            });

        var result = await adapter.ExecuteAsync(provider, new ProviderRequestContext
        {
            Messages =
            [
                new ProviderRequestMessage { Role = "user", Content = "List the files." }
            ],
            EnabledToolCategories = ["filesystem"],
            CancellationToken = CancellationToken.None
        });

        Assert.True(result.IsSuccess);
        Assert.Equal(WorkflowIntentKind.ExecuteTool, result.Intent?.Kind);
        Assert.Equal("list_directory", result.Intent?.Tool?.ToolName);
        Assert.Equal(42, result.TokensUsed);
        Assert.Equal(["filesystem"], provider.LastEnabledToolCategories);
        Assert.Single(provider.LastMessages);
        Assert.Equal("List the files.", provider.LastMessages[0].Content);
    }

    private static ProviderConfig GetConfig(CodexProvider provider)
        => (ProviderConfig)typeof(BaseAiProvider)
            .GetProperty("Config", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .GetValue(provider)!;

    private sealed class FakeCodexProvider : IAiProvider
    {
        private readonly ProviderValidationResult _validationResult;
        private readonly AiResponse _response;

        public FakeCodexProvider(ProviderValidationResult validationResult, AiResponse? response = null)
        {
            _validationResult = validationResult;
            _response = response ?? new AiResponse();
        }

        public string ProviderType => "Codex";
        public string DisplayName => "Codex";
        public ProviderCapabilities Capabilities => ProviderCapabilities.TextChat;
        public ToolCallMode ToolCallMode => ToolCallMode.TextBased;
        public ToolOutputFormat ToolOutputFormat => ToolOutputFormat.AireText;
        public System.Collections.Generic.IReadOnlyList<string>? LastEnabledToolCategories { get; private set; }
        public System.Collections.Generic.IReadOnlyList<ChatMessage> LastMessages { get; private set; } = [];

        public bool Has(ProviderCapabilities cap) => (Capabilities & cap) == cap;
        public void Initialize(ProviderConfig config) { }
        public Task<AiResponse> SendChatAsync(System.Collections.Generic.IEnumerable<ChatMessage> messages, CancellationToken cancellationToken = default)
        {
            LastMessages = messages is System.Collections.Generic.IReadOnlyList<ChatMessage> list
                ? list
                : [.. messages];
            return Task.FromResult(_response);
        }
        public void PrepareForCapabilityTesting() { }
        public void SetToolsEnabled(bool enabled) { }
        public void SetEnabledToolCategories(System.Collections.Generic.IEnumerable<string>? categories)
            => LastEnabledToolCategories = categories is null ? null : [.. categories];
        public async IAsyncEnumerable<string> StreamChatAsync(System.Collections.Generic.IEnumerable<ChatMessage> messages, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield break;
        }
        public Task<ProviderValidationResult> ValidateConfigurationAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(_validationResult);
        public Task<TokenUsage?> GetTokenUsageAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<TokenUsage?>(null);
    }
}
