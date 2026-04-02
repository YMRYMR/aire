using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Aire.AppLayer.Abstractions;
using Aire.Domain.Providers;
using Aire.Providers;
using Aire.Services.Providers;
using Xunit;

namespace Aire.Tests.Services;

public sealed class LegacyProviderAdapterTests
{
    [Fact]
    public void BuildProvider_DelegatesToRuntimeGateway()
    {
        var expectedProvider = new FakeProvider();
        var runtimeGateway = new RecordingRuntimeGateway
        {
            BuildProviderResult = expectedProvider
        };
        var adapter = new LegacyProviderAdapter(runtimeGateway);
        var request = new ProviderRuntimeRequest("OpenAI", "key", "https://example.test", "gpt-5.4-mini", false);

        var provider = adapter.BuildProvider(request);

        Assert.Same(expectedProvider, provider);
        Assert.Equal(request, runtimeGateway.LastBuildRequest);
    }

    [Fact]
    public async Task ExecuteAsync_MapsMessagesAndToolCategoriesThroughLegacyProvider()
    {
        var adapter = new LegacyProviderAdapter(new RecordingRuntimeGateway());
        var provider = new FakeProvider
        {
            Response = new AiResponse
            {
                IsSuccess = true,
                Content = "<tool_call>{\"tool\":\"read_file\",\"path\":\"C:\\\\dev\\\\aire\\\\README.md\"}</tool_call>",
                TokensUsed = 12
            }
        };

        var result = await adapter.ExecuteAsync(provider, new ProviderRequestContext
        {
            Messages =
            [
                new ProviderRequestMessage { Role = "system", Content = "Follow tool rules." },
                new ProviderRequestMessage { Role = "user", Content = "Read the README." }
            ],
            EnabledToolCategories = ["filesystem", "search"],
            CancellationToken = CancellationToken.None
        });

        Assert.True(result.IsSuccess);
        Assert.Equal(WorkflowIntentKind.ExecuteTool, result.Intent?.Kind);
        Assert.Equal("read_file", result.Intent?.Tool?.ToolName);
        Assert.Equal(12, result.TokensUsed);
        Assert.Equal(["filesystem", "search"], provider.LastEnabledToolCategories);
        Assert.Equal(2, provider.LastMessages.Count);
        Assert.Equal("system", provider.LastMessages[0].Role);
        Assert.Equal("Follow tool rules.", provider.LastMessages[0].Content);
        Assert.Equal("user", provider.LastMessages[1].Role);
        Assert.Equal("Read the README.", provider.LastMessages[1].Content);
    }

    [Fact]
    public async Task RunSmokeTestAsync_DelegatesToRuntimeGateway()
    {
        var provider = new FakeProvider();
        var expected = new ProviderSmokeTestResult(true);
        var runtimeGateway = new RecordingRuntimeGateway
        {
            SmokeTestResult = expected
        };
        var adapter = new LegacyProviderAdapter(runtimeGateway);

        var result = await adapter.RunSmokeTestAsync(provider, CancellationToken.None);

        Assert.Equal(expected, result);
        Assert.Same(provider, runtimeGateway.LastSmokeTestProvider);
    }

    [Fact]
    public async Task ValidateAsync_MapsLegacyValidationResultToClassifiedOutcome()
    {
        var adapter = new LegacyProviderAdapter(new RecordingRuntimeGateway());
        var provider = new FakeProvider
        {
            ValidationResult = ProviderValidationResult.Fail("Insufficient balance or no resource package.")
        };

        var outcome = await adapter.ValidateAsync(provider, CancellationToken.None);

        Assert.False(outcome.IsValid);
        Assert.Equal(ProviderValidationFailureKind.BillingError, outcome.FailureKind);
        Assert.Contains("credits", outcome.RemediationHint ?? string.Empty, System.StringComparison.OrdinalIgnoreCase);
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
        public string ProviderType => "LegacyTest";
        public string DisplayName => "LegacyTest";
        public ProviderCapabilities Capabilities => ProviderCapabilities.TextChat | ProviderCapabilities.ToolCalling;
        public ToolCallMode ToolCallMode => ToolCallMode.TextBased;
        public ToolOutputFormat ToolOutputFormat => ToolOutputFormat.AireText;
        public AiResponse Response { get; init; } = new() { IsSuccess = true, Content = "ok" };
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

        public async IAsyncEnumerable<string> StreamChatAsync(
            IEnumerable<ChatMessage> messages,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
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
