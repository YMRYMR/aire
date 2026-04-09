using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Aire.AppLayer.Abstractions;
using Aire.AppLayer.Providers;
using Aire.Domain.Providers;
using Aire.Providers;
using Aire.Services;
using Xunit;

namespace Aire.Tests.Services;

public sealed class ProviderCapabilityTestApplicationServiceTests
{
    private sealed class FakeSettingsRepository : ISettingsRepository
    {
        private readonly Dictionary<string, string> _values = new();

        public Task<string?> GetSettingAsync(string key)
            => Task.FromResult(_values.TryGetValue(key, out var value) ? value : null);

        public Task SetSettingAsync(string key, string value)
        {
            _values[key] = value;
            return Task.CompletedTask;
        }

        public Task LogFileAccessAsync(string operation, string path, bool allowed)
            => Task.CompletedTask;
    }

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
    public async Task ValidateRunAndPersistAsync_BlocksInvalidValidation()
    {
        var gateway = new FakeProviderRuntimeGateway();
        var runtime = new ProviderRuntimeApplicationService(gateway);
        var setup = new ProviderSetupApplicationService(runtime);
        var service = new ProviderCapabilityTestApplicationService(setup);
        var repository = new FakeSettingsRepository();
        var provider = new StubProvider(validationSuccess: false, validationError: "Bad key");

        var result = await service.ValidateRunAndPersistAsync(
            provider,
            providerId: 5,
            model: "stub-model",
            RunOneResult,
            repository,
            progress: null,
            CancellationToken.None);

        Assert.False(result.Started);
        Assert.Equal("Validation failed: Bad key", result.BlockingMessage);
        Assert.Null(await repository.GetSettingAsync("capability_tests_5"));
    }

    [Fact]
    public async Task ValidateRunAndPersistAsync_BlocksAuthSmokeFailures()
    {
        var gateway = new FakeProviderRuntimeGateway
        {
            SmokeTestResult = new ProviderSmokeTestResult(false, "Invalid API key")
        };
        var runtime = new ProviderRuntimeApplicationService(gateway);
        var setup = new ProviderSetupApplicationService(runtime);
        var service = new ProviderCapabilityTestApplicationService(setup);
        var repository = new FakeSettingsRepository();
        var provider = new StubProvider(validationSuccess: true);

        var result = await service.ValidateRunAndPersistAsync(
            provider,
            providerId: 6,
            model: "stub-model",
            RunOneResult,
            repository,
            progress: null,
            CancellationToken.None);

        Assert.False(result.Started);
        Assert.Equal("Test run failed: Invalid API key", result.BlockingMessage);
        Assert.Null(await repository.GetSettingAsync("capability_tests_6"));
    }

    [Fact]
    public async Task ValidateRunAndPersistAsync_PreservesNonAuthSmokeWarnings_AndPersistsResults()
    {
        var gateway = new FakeProviderRuntimeGateway
        {
            SmokeTestResult = new ProviderSmokeTestResult(false, "Rate limit reached")
        };
        var runtime = new ProviderRuntimeApplicationService(gateway);
        var setup = new ProviderSetupApplicationService(runtime);
        var service = new ProviderCapabilityTestApplicationService(setup);
        var repository = new FakeSettingsRepository();
        var provider = new StubProvider(validationSuccess: true);
        var progressUpdates = new List<ProviderCapabilityTestApplicationService.ProgressUpdate>();

        var result = await service.ValidateRunAndPersistAsync(
            provider,
            providerId: 7,
            model: "stub-model",
            RunOneResult,
            repository,
            new Progress<ProviderCapabilityTestApplicationService.ProgressUpdate>(progressUpdates.Add),
            CancellationToken.None);

        Assert.True(result.Started);
        Assert.Equal("Smoke test warning: Rate limit reached", result.WarningMessage);
        Assert.Single(result.Results);
        Assert.Single(progressUpdates);
        Assert.NotNull(result.TestedAt);
        Assert.NotNull(await repository.GetSettingAsync("capability_tests_7"));
    }

    [Fact]
    public async Task RunSingleAndPersistAsync_ReplacesOnlyTheMatchingSavedResult()
    {
        var repository = new FakeSettingsRepository();
        var sessionService = new ProviderCapabilityTestSessionService();
        var provider = new StubProvider(validationSuccess: true);
        var service = new ProviderCapabilityTestApplicationService(new ProviderSetupApplicationService());
        var originalResults = new List<CapabilityTestResult>
        {
            new("ask_followup", "Ask follow-up question", "Agent", false, null, "old failure", 10L),
            new("list_dir", "List directory", "File System", false, null, "stale failure", 15L),
        };

        await sessionService.SaveAsync(11, "stub-model", originalResults, DateTime.UtcNow.AddMinutes(-1), repository);

        var rerunTest = new CapabilityTest(
            "list_dir",
            "List directory",
            "File System",
            "List all files and folders in the C:\\Windows directory.",
            new[] { "list_directory", "execute_command" });

        var rerunResult = await service.RunSingleAndPersistAsync(
            provider,
            providerId: 11,
            model: "stub-model",
            rerunTest,
            (_, test, _) => Task.FromResult(new CapabilityTestResult(
                test.Id,
                test.Name,
                test.Category,
                true,
                "list_directory",
                null,
                42L)),
            repository,
            CancellationToken.None);

        Assert.Equal("list_directory", rerunResult.Result.ActualTool);

        var loaded = await sessionService.LoadAsync(11, "stub-model", repository);
        Assert.NotNull(loaded);
        Assert.Equal(2, loaded!.Results.Count);
        Assert.Equal("old failure", loaded.Results.Single(r => r.Id == "ask_followup").Error);

        var updated = loaded.Results.Single(r => r.Id == "list_dir");
        Assert.True(updated.Passed);
        Assert.Equal("list_directory", updated.ActualTool);
        Assert.Null(updated.Error);
    }

    private static async IAsyncEnumerable<CapabilityTestResult> RunOneResult(
        IAiProvider provider,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        yield return new CapabilityTestResult(
            "list_dir",
            "List directory",
            "File System",
            true,
            "list_directory",
            null,
            12L);
        await Task.CompletedTask;
    }
}
