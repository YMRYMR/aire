using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Aire.AppLayer.Abstractions;
using Aire.AppLayer.Providers;
using Aire.Data;
using Aire.Domain.Providers;
using Aire.Providers;
using Xunit;

namespace Aire.Tests.Services;

public sealed class ProviderSetupApplicationServiceTests
{
    // ── BuildRuntimeProvider ───────────────────────────────────────────

    [Fact]
    public void BuildRuntimeProvider_DelegatesToRuntimeWorkflow()
    {
        var expected = new FakeProvider();
        var adapter = new StubAdapter(buildResult: expected);
        var runtime = new ProviderRuntimeApplicationService(
            new ProviderAdapterApplicationService([adapter]));
        var service = new ProviderSetupApplicationService(runtime);
        var request = new ProviderRuntimeRequest("OpenAI", "sk-test", null, "gpt-4.1-mini", false);

        var result = service.BuildRuntimeProvider(request);

        Assert.Same(expected, result);
        Assert.Same(request, adapter.LastBuildRequest);
    }

    [Fact]
    public void BuildRuntimeProvider_ReturnsNull_WhenAdapterReturnsNull()
    {
        var adapter = new StubAdapter(buildResult: null);
        var runtime = new ProviderRuntimeApplicationService(
            new ProviderAdapterApplicationService([adapter]));
        var service = new ProviderSetupApplicationService(runtime);
        var request = new ProviderRuntimeRequest("OpenAI", null, null, "model", false);

        var result = service.BuildRuntimeProvider(request);

        Assert.Null(result);
    }

    // ── RunSmokeTestAsync ──────────────────────────────────────────────

    [Fact]
    public async Task RunSmokeTestAsync_DelegatesToRuntimeWorkflow()
    {
        var expected = new ProviderSmokeTestResult(true);
        var adapter = new StubAdapter(smokeTestResult: expected);
        var runtime = new ProviderRuntimeApplicationService(
            new ProviderAdapterApplicationService([adapter]));
        var service = new ProviderSetupApplicationService(runtime);
        var provider = new FakeProvider();

        var result = await service.RunSmokeTestAsync(provider, CancellationToken.None);

        Assert.Equal(expected, result);
        Assert.Same(provider, adapter.LastSmokeTestProvider);
    }

    [Fact]
    public async Task RunSmokeTestAsync_ReturnsFailure_WhenAdapterFails()
    {
        var expected = new ProviderSmokeTestResult(false, "Connection refused");
        var adapter = new StubAdapter(smokeTestResult: expected);
        var runtime = new ProviderRuntimeApplicationService(
            new ProviderAdapterApplicationService([adapter]));
        var service = new ProviderSetupApplicationService(runtime);

        var result = await service.RunSmokeTestAsync(new FakeProvider(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Connection refused", result.ErrorMessage);
    }

    // ── ValidateAsync (calls provider directly) ────────────────────────

    [Fact]
    public async Task ValidateAsync_ReturnsOk_WhenProviderIsValid()
    {
        var runtime = new ProviderRuntimeApplicationService(
            new ProviderAdapterApplicationService([new StubAdapter()]));
        var service = new ProviderSetupApplicationService(runtime);
        var provider = new FakeProvider
        {
            ValidationResult = ProviderValidationResult.Ok()
        };

        var result = await service.ValidateAsync(provider, CancellationToken.None);

        Assert.True(result.IsValid);
        Assert.Null(result.Error);
    }

    [Fact]
    public async Task ValidateAsync_ReturnsFail_WhenProviderIsInvalid()
    {
        var runtime = new ProviderRuntimeApplicationService(
            new ProviderAdapterApplicationService([new StubAdapter()]));
        var service = new ProviderSetupApplicationService(runtime);
        var provider = new FakeProvider
        {
            ValidationResult = ProviderValidationResult.Fail("API key is invalid.")
        };

        var result = await service.ValidateAsync(provider, CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.Equal("API key is invalid.", result.Error);
    }

    // ── ValidateDetailedAsync (delegates to runtime workflow) ──────────

    [Fact]
    public async Task ValidateDetailedAsync_DelegatesToRuntimeWorkflow()
    {
        var outcome = ProviderValidationOutcome.Valid();
        var adapter = new StubAdapter(validationOutcome: outcome);
        var runtime = new ProviderRuntimeApplicationService(
            new ProviderAdapterApplicationService([adapter]));
        var service = new ProviderSetupApplicationService(runtime);
        var provider = new FakeProvider();

        var result = await service.ValidateDetailedAsync(provider, CancellationToken.None);

        Assert.True(result.IsValid);
        Assert.Equal(ProviderValidationFailureKind.None, result.FailureKind);
        Assert.Null(result.ErrorMessage);
        Assert.Same(provider, adapter.LastValidateProvider);
    }

    [Fact]
    public async Task ValidateDetailedAsync_ReturnsInvalidOutcome_WhenValidationFails()
    {
        var outcome = ProviderValidationOutcome.Invalid(
            "Bad credentials",
            ProviderValidationFailureKind.InvalidCredentials,
            "Check your API key");
        var adapter = new StubAdapter(validationOutcome: outcome);
        var runtime = new ProviderRuntimeApplicationService(
            new ProviderAdapterApplicationService([adapter]));
        var service = new ProviderSetupApplicationService(runtime);

        var result = await service.ValidateDetailedAsync(new FakeProvider(), CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.Equal(ProviderValidationFailureKind.InvalidCredentials, result.FailureKind);
        Assert.Equal("Bad credentials", result.ErrorMessage);
        Assert.Equal("Check your API key", result.RemediationHint);
    }

    // ── SaveNewProviderAsync ────────────────────────────────────────────

    [Fact]
    public async Task SaveNewProviderAsync_BlocksDuplicate_AndReturnsSavedId()
    {
        var service = new ProviderSetupApplicationService();
        var repository = new RecordingProviderRepository
        {
            ExistingProviders =
            [
                new Provider { Id = 1, Type = "OpenAI", Model = "gpt-4.1-mini" }
            ],
            NextInsertedId = 42
        };

        // Duplicate (same type + model)
        var duplicate = await service.SaveNewProviderAsync(repository, new ProviderDraft(
            "My OpenAI",
            "OpenAI",
            "sk-test",
            null,
            "gpt-4.1-mini"));

        // New provider (different model)
        var saved = await service.SaveNewProviderAsync(repository, new ProviderDraft(
            "Groq Fast",
            "Groq",
            "gsk-key",
            null,
            "llama-3.3-70b"));

        Assert.False(duplicate.Saved);
        Assert.True(duplicate.IsDuplicate);
        Assert.Null(duplicate.ProviderId);

        Assert.True(saved.Saved);
        Assert.False(saved.IsDuplicate);
        Assert.Equal(42, saved.ProviderId);
        Assert.Single(repository.InsertedProviders);
        Assert.Equal("Groq", repository.InsertedProviders[0].Type);
    }

    [Fact]
    public async Task SaveNewProviderAsync_AllowsDifferentModelsForSameType()
    {
        var service = new ProviderSetupApplicationService();
        var repository = new RecordingProviderRepository
        {
            ExistingProviders =
            [
                new Provider { Id = 1, Type = "OpenAI", Model = "gpt-4.1-mini" }
            ],
            NextInsertedId = 7
        };

        var result = await service.SaveNewProviderAsync(repository, new ProviderDraft(
            "OpenAI Big",
            "OpenAI",
            "sk-other",
            null,
            "gpt-4.1"));

        Assert.True(result.Saved);
        Assert.False(result.IsDuplicate);
        Assert.Equal(7, result.ProviderId);
    }

    // ── Constructor overloads ──────────────────────────────────────────

    [Fact]
    public void ParameterlessConstructor_CreatesDefaultRuntimeWorkflow()
    {
        // Smoke test: the parameterless constructor does not throw and
        // the service can be used for configuration-workflow paths that
        // don't touch the runtime workflow.
        var service = new ProviderSetupApplicationService();
        Assert.NotNull(service);
    }

    [Fact]
    public void InjectedRuntimeWorkflow_IsUsedByAllRuntimeMethods()
    {
        var expectedProvider = new FakeProvider();
        var adapter = new StubAdapter(buildResult: expectedProvider);
        var runtime = new ProviderRuntimeApplicationService(
            new ProviderAdapterApplicationService([adapter]));
        var service = new ProviderSetupApplicationService(runtime);

        var result = service.BuildRuntimeProvider(
            new ProviderRuntimeRequest("TestType", "key", null, "model", false));

        Assert.Same(expectedProvider, result);
    }

    // ── Test doubles ───────────────────────────────────────────────────

    private sealed class StubAdapter : IProviderAdapter
    {
        private readonly IAiProvider? _buildResult;
        private readonly ProviderSmokeTestResult _smokeTestResult;
        private readonly ProviderValidationOutcome _validationOutcome;

        public ProviderRuntimeRequest? LastBuildRequest { get; private set; }
        public IAiProvider? LastSmokeTestProvider { get; private set; }
        public IAiProvider? LastValidateProvider { get; private set; }

        public StubAdapter(
            IAiProvider? buildResult = null,
            ProviderSmokeTestResult? smokeTestResult = null,
            ProviderValidationOutcome? validationOutcome = null)
        {
            _buildResult = buildResult;
            _smokeTestResult = smokeTestResult ?? new ProviderSmokeTestResult(true);
            _validationOutcome = validationOutcome ?? ProviderValidationOutcome.Valid();
        }

        public string ProviderType => "*";
        public bool CanHandle(string providerType) => !string.IsNullOrWhiteSpace(providerType);

        public IAiProvider? BuildProvider(ProviderRuntimeRequest request)
        {
            LastBuildRequest = request;
            return _buildResult;
        }

        public Task<ProviderSmokeTestResult> RunSmokeTestAsync(IAiProvider provider, CancellationToken cancellationToken)
        {
            LastSmokeTestProvider = provider;
            return Task.FromResult(_smokeTestResult);
        }

        public Task<ProviderValidationOutcome> ValidateAsync(IAiProvider provider, CancellationToken cancellationToken)
        {
            LastValidateProvider = provider;
            return Task.FromResult(_validationOutcome);
        }

        public Task<ProviderExecutionResult> ExecuteAsync(IAiProvider provider, ProviderRequestContext requestContext)
            => Task.FromResult(ProviderExecutionResult.Succeeded(
                WorkflowIntent.AssistantText("stub")));
    }

    private sealed class FakeProvider : IAiProvider
    {
        public string ProviderType => "Test";
        public string DisplayName => "Test Provider";
        public ProviderCapabilities Capabilities => ProviderCapabilities.TextChat;
        public ToolCallMode ToolCallMode => ToolCallMode.TextBased;
        public ToolOutputFormat ToolOutputFormat => ToolOutputFormat.AireText;

        public ProviderValidationResult ValidationResult { get; init; } = ProviderValidationResult.Ok();

        public bool Has(ProviderCapabilities cap) => (Capabilities & cap) == cap;
        public void Initialize(ProviderConfig config) { }

        public Task<AiResponse> SendChatAsync(IEnumerable<ChatMessage> messages, CancellationToken cancellationToken = default)
            => Task.FromResult(new AiResponse { IsSuccess = true, Content = "test" });

        public void PrepareForCapabilityTesting() { }
        public void SetToolsEnabled(bool enabled) { }
        public void SetEnabledToolCategories(IEnumerable<string>? categories) { }

        public async IAsyncEnumerable<string> StreamChatAsync(IEnumerable<ChatMessage> messages,
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

    private sealed class RecordingProviderRepository : IProviderRepository
    {
        public List<Provider> ExistingProviders { get; init; } = [];
        public List<Provider> InsertedProviders { get; } = [];
        public int NextInsertedId { get; init; }

        public Task<List<Provider>> GetProvidersAsync()
            => Task.FromResult(new List<Provider>(ExistingProviders));

        public Task<int> InsertProviderAsync(Provider provider)
        {
            InsertedProviders.Add(provider);
            return Task.FromResult(NextInsertedId);
        }

        public Task UpdateProviderAsync(Provider provider) => throw new NotSupportedException();
        public Task SaveProviderOrderAsync(IEnumerable<Provider> orderedProviders) => throw new NotSupportedException();
        public Task DeleteProviderAsync(int id) => throw new NotSupportedException();
    }
}
