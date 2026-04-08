using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Aire.AppLayer.Abstractions;
using Aire.AppLayer.Providers;
using Aire.Domain.Providers;
using Aire.Providers;
using Xunit;

namespace Aire.Tests.Services;

public sealed class ProviderConnectionTestApplicationServiceTests
{
    private sealed class FakeProviderRuntimeGateway : IProviderRuntimeGateway
    {
        public ProviderSmokeTestResult SmokeTestResult { get; set; } = new(true);

        public IAiProvider? BuildProvider(ProviderRuntimeRequest request)
            => null;

        public Task<ProviderSmokeTestResult> RunSmokeTestAsync(IAiProvider provider, CancellationToken cancellationToken)
            => Task.FromResult(SmokeTestResult);
    }

    private sealed class StubProvider(bool validationSuccess, string? validationError = null) : IAiProvider
    {
        public string ProviderType => "Stub";
        public string DisplayName => "Stub";
        public ProviderCapabilities Capabilities => ProviderCapabilities.SystemPrompt;
        public ToolCallMode ToolCallMode => ToolCallMode.TextBased;
        public ToolOutputFormat ToolOutputFormat => ToolOutputFormat.AireText;

        public bool Has(ProviderCapabilities cap) => (Capabilities & cap) == cap;
        public void Initialize(ProviderConfig config) { }
        public void PrepareForCapabilityTesting() { }
        public void SetToolsEnabled(bool enabled) { }
        public void SetEnabledToolCategories(IEnumerable<string>? categories) { }

        public Task<AiResponse> SendChatAsync(IEnumerable<ChatMessage> messages, CancellationToken cancellationToken = default)
            => Task.FromResult(new AiResponse { IsSuccess = true, Content = "ok" });

        public async IAsyncEnumerable<string> StreamChatAsync(IEnumerable<ChatMessage> messages, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            yield break;
        }

        public Task<ProviderValidationResult> ValidateConfigurationAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(validationSuccess ? ProviderValidationResult.Ok() : ProviderValidationResult.Fail(validationError ?? "invalid"));

        public Task<TokenUsage?> GetTokenUsageAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<TokenUsage?>(null);
    }

    [Fact]
    public async Task RunAsync_ReturnsValidationFailureMessage()
    {
        var gateway = new FakeProviderRuntimeGateway();
        var runtime = new ProviderRuntimeApplicationService(gateway);
        var setup = new ProviderSetupApplicationService(runtime);
        var service = new ProviderConnectionTestApplicationService(setup);

        var result = await service.RunAsync(new StubProvider(false, "Bad key"), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Bad key", result.Message);
    }

    [Fact]
    public async Task RunAsync_AppendsRemediationHint_WhenValidationSuggestsBillingIssue()
    {
        var gateway = new FakeProviderRuntimeGateway();
        var runtime = new ProviderRuntimeApplicationService(gateway);
        var setup = new ProviderSetupApplicationService(runtime);
        var service = new ProviderConnectionTestApplicationService(setup);

        var result = await service.RunAsync(
            new StubProvider(false, "Insufficient balance or no resource package. Please recharge."),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("credits", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Please recharge", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RunAsync_ReturnsSmokeFailureMessage()
    {
        var gateway = new FakeProviderRuntimeGateway
        {
            SmokeTestResult = new ProviderSmokeTestResult(false, "Timed out")
        };
        var runtime = new ProviderRuntimeApplicationService(gateway);
        var setup = new ProviderSetupApplicationService(runtime);
        var service = new ProviderConnectionTestApplicationService(setup);

        var result = await service.RunAsync(new StubProvider(true), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Timed out", result.Message);
    }

    [Fact]
    public async Task RunAsync_ReturnsConnectedOnSuccess()
    {
        var gateway = new FakeProviderRuntimeGateway();
        var runtime = new ProviderRuntimeApplicationService(gateway);
        var setup = new ProviderSetupApplicationService(runtime);
        var service = new ProviderConnectionTestApplicationService(setup);

        var result = await service.RunAsync(new StubProvider(true), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("Connected!", result.Message);
    }
}
