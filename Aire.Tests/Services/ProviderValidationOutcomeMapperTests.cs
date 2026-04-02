using System.Threading;
using System.Threading.Tasks;
using Aire.AppLayer.Abstractions;
using Aire.AppLayer.Providers;
using Aire.Domain.Providers;
using Aire.Providers;
using Xunit;

namespace Aire.Tests.Services;

public sealed class ProviderValidationOutcomeMapperTests
{
    [Theory]
    [InlineData("Invalid API key provided.", ProviderValidationFailureKind.InvalidCredentials)]
    [InlineData("Unauthorized request.", ProviderValidationFailureKind.InvalidCredentials)]
    [InlineData("Network timeout while contacting provider.", ProviderValidationFailureKind.NetworkError)]
    [InlineData("DNS lookup failed.", ProviderValidationFailureKind.NetworkError)]
    [InlineData("Rate limit exceeded.", ProviderValidationFailureKind.RateLimit)]
    [InlineData("Too many requests.", ProviderValidationFailureKind.RateLimit)]
    [InlineData("Insufficient balance or no resource package. Please recharge.", ProviderValidationFailureKind.BillingError)]
    [InlineData("Billing problem detected.", ProviderValidationFailureKind.BillingError)]
    [InlineData("Service unavailable.", ProviderValidationFailureKind.ServiceUnavailable)]
    [InlineData("Bad gateway from upstream.", ProviderValidationFailureKind.ServiceUnavailable)]
    [InlineData("Something odd happened.", ProviderValidationFailureKind.Unknown)]
    public void FromLegacyResult_ClassifiesKnownErrors(string errorMessage, ProviderValidationFailureKind expectedKind)
    {
        var outcome = ProviderValidationOutcomeMapper.FromLegacyResult(ProviderValidationResult.Fail(errorMessage));

        Assert.False(outcome.IsValid);
        Assert.Equal(errorMessage, outcome.ErrorMessage);
        Assert.Equal(expectedKind, outcome.FailureKind);
    }

    [Fact]
    public void FromLegacyResult_MapsSuccessToValidOutcome()
    {
        var outcome = ProviderValidationOutcomeMapper.FromLegacyResult(ProviderValidationResult.Ok());

        Assert.True(outcome.IsValid);
        Assert.Equal(ProviderValidationFailureKind.None, outcome.FailureKind);
        Assert.Null(outcome.ErrorMessage);
        Assert.Null(outcome.RemediationHint);
    }

    [Fact]
    public async Task ProviderSetupApplicationService_ValidateDetailedAsync_ReturnsClassifiedOutcome()
    {
        var adapterService = new ProviderAdapterApplicationService(new IProviderAdapter[]
        {
            new ValidationOnlyAdapter()
        });
        var runtimeWorkflow = new ProviderRuntimeApplicationService(adapterService);
        var service = new ProviderSetupApplicationService(runtimeWorkflow);
        var provider = new FakeProvider("Codex", ProviderValidationResult.Fail("Insufficient balance or no resource package."));

        var outcome = await service.ValidateDetailedAsync(provider, CancellationToken.None);

        Assert.False(outcome.IsValid);
        Assert.Equal(ProviderValidationFailureKind.BillingError, outcome.FailureKind);
        Assert.Contains("credits", outcome.RemediationHint ?? string.Empty, System.StringComparison.OrdinalIgnoreCase);
    }

    private sealed class ValidationOnlyAdapter : IProviderAdapter
    {
        public string ProviderType => "Codex";

        public bool CanHandle(string providerType)
            => string.Equals(providerType, ProviderType, System.StringComparison.OrdinalIgnoreCase);

        public IAiProvider? BuildProvider(ProviderRuntimeRequest request) => null;

        public Task<ProviderExecutionResult> ExecuteAsync(IAiProvider provider, ProviderRequestContext requestContext)
            => Task.FromResult(ProviderExecutionResult.Succeeded(WorkflowIntent.AssistantText("unused")));

        public Task<ProviderSmokeTestResult> RunSmokeTestAsync(IAiProvider provider, CancellationToken cancellationToken)
            => Task.FromResult(new ProviderSmokeTestResult(true, null));

        public async Task<ProviderValidationOutcome> ValidateAsync(IAiProvider provider, CancellationToken cancellationToken)
        {
            var result = await provider.ValidateConfigurationAsync(cancellationToken);
            return ProviderValidationOutcomeMapper.FromLegacyResult(result);
        }
    }

    private sealed class FakeProvider : IAiProvider
    {
        private readonly ProviderValidationResult _validationResult;

        public FakeProvider(string providerType, ProviderValidationResult validationResult)
        {
            ProviderType = providerType;
            _validationResult = validationResult;
        }

        public string ProviderType { get; }
        public string DisplayName => ProviderType;
        public ProviderCapabilities Capabilities => ProviderCapabilities.TextChat;
        public ToolCallMode ToolCallMode => ToolCallMode.TextBased;
        public ToolOutputFormat ToolOutputFormat => ToolOutputFormat.AireText;
        public bool Has(ProviderCapabilities cap) => (Capabilities & cap) == cap;
        public void Initialize(ProviderConfig config) { }
        public Task<AiResponse> SendChatAsync(System.Collections.Generic.IEnumerable<ChatMessage> messages, CancellationToken cancellationToken = default)
            => Task.FromResult(new AiResponse());
        public void PrepareForCapabilityTesting() { }
        public void SetToolsEnabled(bool enabled) { }
        public void SetEnabledToolCategories(System.Collections.Generic.IEnumerable<string>? categories) { }
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
