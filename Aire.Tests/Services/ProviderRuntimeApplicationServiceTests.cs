using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Aire.AppLayer.Abstractions;
using Aire.AppLayer.Providers;
using Aire.Domain.Providers;
using Aire.Providers;
using Xunit;

namespace Aire.Tests.Services;

public sealed class ProviderRuntimeApplicationServiceTests
{
    [Fact]
    public async Task ExecuteAsync_DelegatesToResolvedAdapter()
    {
        var adapter = new RecordingAdapter();
        var service = new ProviderRuntimeApplicationService(new ProviderAdapterApplicationService([adapter]));
        var provider = new FakeProvider();
        var requestContext = new ProviderRequestContext
        {
            Messages =
            [
                new ProviderRequestMessage { Role = "user", Content = "hello" }
            ],
            CancellationToken = CancellationToken.None
        };

        var result = await service.ExecuteAsync(provider, requestContext);

        Assert.True(result.IsSuccess);
        Assert.Equal(WorkflowIntentKind.AssistantText, result.Intent?.Kind);
        Assert.Same(provider, adapter.LastProvider);
        Assert.Same(requestContext, adapter.LastRequestContext);
    }

    [Fact]
    public void BuildProvider_WithRuntimeGatewayOverload_UsesGatewayBridge()
    {
        var expectedProvider = new FakeProvider();
        var gateway = new RecordingRuntimeGateway
        {
            BuildProviderResult = expectedProvider
        };
        var service = new ProviderRuntimeApplicationService(gateway);
        var request = new ProviderRuntimeRequest("Anything", "key", "https://example.test", "model-x", false);

        var provider = service.BuildProvider(request);

        Assert.Same(expectedProvider, provider);
        Assert.Equal(request, gateway.LastBuildRequest);
    }

    [Fact]
    public async Task RunSmokeTestAsync_WithRuntimeGatewayOverload_UsesGatewayBridge()
    {
        var provider = new FakeProvider();
        var expected = new ProviderSmokeTestResult(true, null);
        var gateway = new RecordingRuntimeGateway
        {
            SmokeTestResult = expected
        };
        var service = new ProviderRuntimeApplicationService(gateway);

        var result = await service.RunSmokeTestAsync(provider, CancellationToken.None);

        Assert.Equal(expected, result);
        Assert.Same(provider, gateway.LastSmokeTestProvider);
    }

    [Fact]
    public async Task ValidateAsync_WithRuntimeGatewayOverload_MapsLegacyValidationResult()
    {
        var provider = new FakeProvider
        {
            ValidationResult = ProviderValidationResult.Fail("Unauthorized request.")
        };
        var service = new ProviderRuntimeApplicationService(new RecordingRuntimeGateway());

        var outcome = await service.ValidateAsync(provider, CancellationToken.None);

        Assert.False(outcome.IsValid);
        Assert.Equal(ProviderValidationFailureKind.InvalidCredentials, outcome.FailureKind);
    }

    [Fact]
    public async Task ExecuteAsync_WithRuntimeGatewayOverload_MapsLegacyExecutionResult()
    {
        var provider = new FakeProvider
        {
            Response = new AiResponse
            {
                IsSuccess = true,
                Content = "<tool_call>{\"tool\":\"search_files\",\"pattern\":\"*.cs\",\"directory\":\"C:\\\\dev\\\\aire\"}</tool_call>",
                TokensUsed = 9
            }
        };
        var service = new ProviderRuntimeApplicationService(new RecordingRuntimeGateway());

        var result = await service.ExecuteAsync(provider, new ProviderRequestContext
        {
            Messages =
            [
                new ProviderRequestMessage { Role = "user", Content = "Search the repo." }
            ],
            EnabledToolCategories = ["search"],
            CancellationToken = CancellationToken.None
        });

        Assert.True(result.IsSuccess);
        Assert.Equal(WorkflowIntentKind.ExecuteTool, result.Intent?.Kind);
        Assert.Equal("search_files", result.Intent?.Tool?.ToolName);
        Assert.Equal(["search"], provider.LastEnabledToolCategories);
        Assert.Single(provider.LastMessages);
    }

    private sealed class RecordingAdapter : IProviderAdapter
    {
        public string ProviderType => "Codex";
        public IAiProvider? LastProvider { get; private set; }
        public ProviderRequestContext? LastRequestContext { get; private set; }

        public bool CanHandle(string providerType)
            => providerType == ProviderType;

        public IAiProvider? BuildProvider(ProviderRuntimeRequest request) => null;

        public Task<ProviderExecutionResult> ExecuteAsync(IAiProvider provider, ProviderRequestContext requestContext)
        {
            LastProvider = provider;
            LastRequestContext = requestContext;
            return Task.FromResult(ProviderExecutionResult.Succeeded(WorkflowIntent.AssistantText("done")));
        }

        public Task<ProviderSmokeTestResult> RunSmokeTestAsync(IAiProvider provider, CancellationToken cancellationToken)
            => Task.FromResult(new ProviderSmokeTestResult(true));

        public Task<ProviderValidationOutcome> ValidateAsync(IAiProvider provider, CancellationToken cancellationToken)
            => Task.FromResult(ProviderValidationOutcome.Valid());
    }

    private sealed class RecordingRuntimeGateway : IProviderRuntimeGateway
    {
        public ProviderRuntimeRequest? LastBuildRequest { get; private set; }
        public IAiProvider? BuildProviderResult { get; init; }
        public IAiProvider? LastSmokeTestProvider { get; private set; }
        public ProviderSmokeTestResult SmokeTestResult { get; init; } = new(true);

        public IAiProvider? BuildProvider(ProviderRuntimeRequest request)
        {
            LastBuildRequest = request;
            return BuildProviderResult;
        }

        public Task<ProviderSmokeTestResult> RunSmokeTestAsync(IAiProvider provider, CancellationToken cancellationToken)
        {
            LastSmokeTestProvider = provider;
            return Task.FromResult(SmokeTestResult);
        }
    }

    private sealed class FakeProvider : IAiProvider
    {
        public string ProviderType => "Codex";
        public string DisplayName => "Codex";
        public ProviderCapabilities Capabilities => ProviderCapabilities.TextChat;
        public ToolCallMode ToolCallMode => ToolCallMode.TextBased;
        public ToolOutputFormat ToolOutputFormat => ToolOutputFormat.AireText;
        public AiResponse Response { get; init; } = new();
        public ProviderValidationResult ValidationResult { get; init; } = ProviderValidationResult.Ok();
        public IReadOnlyList<string>? LastEnabledToolCategories { get; private set; }
        public IReadOnlyList<ChatMessage> LastMessages { get; private set; } = [];

        public bool Has(ProviderCapabilities cap) => (Capabilities & cap) == cap;
        public void Initialize(ProviderConfig config) { }
        public Task<AiResponse> SendChatAsync(IEnumerable<ChatMessage> messages, CancellationToken cancellationToken = default)
        {
            LastMessages = messages is IReadOnlyList<ChatMessage> list ? list : [.. messages];
            return Task.FromResult(Response);
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
            => Task.FromResult(ValidationResult);
        public Task<TokenUsage?> GetTokenUsageAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<TokenUsage?>(null);
    }
}
