using Aire.AppLayer.Abstractions;
using Aire.AppLayer.Providers;
using Aire.Data;
using Xunit;

namespace Aire.Tests.Services;

public sealed class OnboardingProviderSetupApplicationServiceTests
{
    private readonly OnboardingProviderSetupApplicationService _service = new();

    [Fact]
    public async Task CompleteStepAsync_EmptyProviderName_ReturnsAdvanceWithoutSave()
    {
        var repo = new FakeProviderRepository();
        var request = new OnboardingProviderSetupApplicationService.Step3Request(
            ProviderName: "",
            ProviderType: "OpenAI",
            ApiKey: "sk-test",
            BaseUrl: null,
            StandardModel: "gpt-4o",
            OllamaModel: null,
            ClaudeWebSessionReady: false);

        var result = await _service.CompleteStepAsync(repo, request);

        Assert.True(result.ShouldAdvance);
        Assert.False(result.SavedProvider);
        Assert.False(result.IsDuplicate);
        Assert.Equal("OpenAI", result.ProviderType);
        Assert.Equal(string.Empty, result.Model);
    }

    [Fact]
    public async Task CompleteStepAsync_WhitespaceProviderName_ReturnsAdvanceWithoutSave()
    {
        var repo = new FakeProviderRepository();
        var request = new OnboardingProviderSetupApplicationService.Step3Request(
            ProviderName: "   ",
            ProviderType: "OpenAI",
            ApiKey: "sk-test",
            BaseUrl: null,
            StandardModel: "gpt-4o",
            OllamaModel: null,
            ClaudeWebSessionReady: false);

        var result = await _service.CompleteStepAsync(repo, request);

        Assert.True(result.ShouldAdvance);
        Assert.False(result.SavedProvider);
    }

    [Fact]
    public async Task CompleteStepAsync_NullProviderName_ReturnsAdvanceWithoutSave()
    {
        var repo = new FakeProviderRepository();
        var request = new OnboardingProviderSetupApplicationService.Step3Request(
            ProviderName: null!,
            ProviderType: "OpenAI",
            ApiKey: "sk-test",
            BaseUrl: null,
            StandardModel: "gpt-4o",
            OllamaModel: null,
            ClaudeWebSessionReady: false);

        var result = await _service.CompleteStepAsync(repo, request);

        Assert.True(result.ShouldAdvance);
        Assert.False(result.SavedProvider);
    }

    [Fact]
    public async Task CompleteStepAsync_NullProviderType_DefaultsToOpenAI()
    {
        var repo = new FakeProviderRepository();
        var request = new OnboardingProviderSetupApplicationService.Step3Request(
            ProviderName: "",
            ProviderType: null!,
            ApiKey: null,
            BaseUrl: null,
            StandardModel: null,
            OllamaModel: null,
            ClaudeWebSessionReady: false);

        var result = await _service.CompleteStepAsync(repo, request);

        Assert.Equal("OpenAI", result.ProviderType);
    }

    [Fact]
    public async Task CompleteStepAsync_MissingApiKeyForCredentialedType_ReturnsAdvanceWithoutSave()
    {
        var repo = new FakeProviderRepository();
        var request = new OnboardingProviderSetupApplicationService.Step3Request(
            ProviderName: "My OpenAI",
            ProviderType: "OpenAI",
            ApiKey: null,
            BaseUrl: null,
            StandardModel: "gpt-4o",
            OllamaModel: null,
            ClaudeWebSessionReady: false);

        var result = await _service.CompleteStepAsync(repo, request);

        Assert.True(result.ShouldAdvance);
        Assert.False(result.SavedProvider);
        Assert.False(result.IsDuplicate);
    }

    [Fact]
    public async Task CompleteStepAsync_EmptyApiKeyForCredentialedType_ReturnsAdvanceWithoutSave()
    {
        var repo = new FakeProviderRepository();
        var request = new OnboardingProviderSetupApplicationService.Step3Request(
            ProviderName: "My Anthropic",
            ProviderType: "Anthropic",
            ApiKey: "   ",
            BaseUrl: null,
            StandardModel: "claude-3-opus",
            OllamaModel: null,
            ClaudeWebSessionReady: false);

        var result = await _service.CompleteStepAsync(repo, request);

        Assert.True(result.ShouldAdvance);
        Assert.False(result.SavedProvider);
    }

    [Fact]
    public async Task CompleteStepAsync_OllamaType_UsesOllamaModel()
    {
        var repo = new FakeProviderRepository();
        var request = new OnboardingProviderSetupApplicationService.Step3Request(
            ProviderName: "Local Ollama",
            ProviderType: "Ollama",
            ApiKey: null,
            BaseUrl: null,
            StandardModel: "gpt-4o",
            OllamaModel: "llama3",
            ClaudeWebSessionReady: false);

        var result = await _service.CompleteStepAsync(repo, request);

        Assert.True(result.ShouldAdvance);
        Assert.True(result.SavedProvider);
        Assert.Equal("Ollama", result.ProviderType);
        Assert.Equal("llama3", result.Model);
    }

    [Fact]
    public async Task CompleteStepAsync_OllamaType_NoApiKeyRequired_SavesSuccessfully()
    {
        var repo = new FakeProviderRepository();
        var request = new OnboardingProviderSetupApplicationService.Step3Request(
            ProviderName: "Ollama Local",
            ProviderType: "Ollama",
            ApiKey: null,
            BaseUrl: null,
            StandardModel: null,
            OllamaModel: "mistral",
            ClaudeWebSessionReady: false);

        var result = await _service.CompleteStepAsync(repo, request);

        Assert.True(result.SavedProvider);
        Assert.Equal("mistral", result.Model);
        Assert.Single(repo.InsertedProviders);
    }

    [Fact]
    public async Task CompleteStepAsync_NonOllamaType_UsesStandardModel()
    {
        var repo = new FakeProviderRepository();
        var request = new OnboardingProviderSetupApplicationService.Step3Request(
            ProviderName: "My GPT",
            ProviderType: "OpenAI",
            ApiKey: "sk-test-key",
            BaseUrl: null,
            StandardModel: "gpt-4o",
            OllamaModel: "llama3",
            ClaudeWebSessionReady: false);

        var result = await _service.CompleteStepAsync(repo, request);

        Assert.True(result.SavedProvider);
        Assert.Equal("OpenAI", result.ProviderType);
        Assert.Equal("gpt-4o", result.Model);
    }

    [Fact]
    public async Task CompleteStepAsync_DuplicateProvider_ReturnsDuplicateNotAdvance()
    {
        var repo = new FakeProviderRepository();
        repo.ExistingProviders.Add(new Provider
        {
            Name = "Existing OpenAI",
            Type = "OpenAI",
            Model = "gpt-4o"
        });

        var request = new OnboardingProviderSetupApplicationService.Step3Request(
            ProviderName: "New OpenAI",
            ProviderType: "OpenAI",
            ApiKey: "sk-dup",
            BaseUrl: null,
            StandardModel: "gpt-4o",
            OllamaModel: null,
            ClaudeWebSessionReady: false);

        var result = await _service.CompleteStepAsync(repo, request);

        Assert.False(result.ShouldAdvance);
        Assert.False(result.SavedProvider);
        Assert.True(result.IsDuplicate);
        Assert.Equal("gpt-4o", result.Model);
    }

    [Fact]
    public async Task CompleteStepAsync_DuplicateProvider_SameTypeDifferentModel_IsNotDuplicate()
    {
        var repo = new FakeProviderRepository();
        repo.ExistingProviders.Add(new Provider
        {
            Name = "Existing OpenAI",
            Type = "OpenAI",
            Model = "gpt-4o"
        });

        var request = new OnboardingProviderSetupApplicationService.Step3Request(
            ProviderName: "New OpenAI",
            ProviderType: "OpenAI",
            ApiKey: "sk-new",
            BaseUrl: null,
            StandardModel: "gpt-4o-mini",
            OllamaModel: null,
            ClaudeWebSessionReady: false);

        var result = await _service.CompleteStepAsync(repo, request);

        Assert.True(result.ShouldAdvance);
        Assert.True(result.SavedProvider);
        Assert.False(result.IsDuplicate);
    }

    [Fact]
    public async Task CompleteStepAsync_SuccessfulSave_ReturnsAdvanceWithSavedProvider()
    {
        var repo = new FakeProviderRepository();
        var request = new OnboardingProviderSetupApplicationService.Step3Request(
            ProviderName: "My Provider",
            ProviderType: "OpenAI",
            ApiKey: "sk-abc123",
            BaseUrl: null,
            StandardModel: "gpt-4o",
            OllamaModel: null,
            ClaudeWebSessionReady: false);

        var result = await _service.CompleteStepAsync(repo, request);

        Assert.True(result.ShouldAdvance);
        Assert.True(result.SavedProvider);
        Assert.False(result.IsDuplicate);
        Assert.Equal("OpenAI", result.ProviderType);
        Assert.Equal("gpt-4o", result.Model);
        Assert.Single(repo.InsertedProviders);
        Assert.Equal("My Provider", repo.InsertedProviders[0].Name);
        Assert.Equal("sk-abc123", repo.InsertedProviders[0].ApiKey);
    }

    [Fact]
    public async Task CompleteStepAsync_WithBaseUrl_SavesProviderWithBaseUrl()
    {
        var repo = new FakeProviderRepository();
        var request = new OnboardingProviderSetupApplicationService.Step3Request(
            ProviderName: "Custom Endpoint",
            ProviderType: "OpenAI",
            ApiKey: "sk-custom",
            BaseUrl: "https://custom.api.com/v1",
            StandardModel: "gpt-4o",
            OllamaModel: null,
            ClaudeWebSessionReady: false);

        var result = await _service.CompleteStepAsync(repo, request);

        Assert.True(result.SavedProvider);
        Assert.Equal("https://custom.api.com/v1", repo.InsertedProviders[0].BaseUrl);
    }

    [Fact]
    public async Task CompleteStepAsync_WhitespaceBaseUrl_SavesNullBaseUrl()
    {
        var repo = new FakeProviderRepository();
        var request = new OnboardingProviderSetupApplicationService.Step3Request(
            ProviderName: "Whitespace URL",
            ProviderType: "OpenAI",
            ApiKey: "sk-test",
            BaseUrl: "   ",
            StandardModel: "gpt-4o",
            OllamaModel: null,
            ClaudeWebSessionReady: false);

        var result = await _service.CompleteStepAsync(repo, request);

        Assert.True(result.SavedProvider);
        Assert.Null(repo.InsertedProviders[0].BaseUrl);
    }

    [Fact]
    public async Task CompleteStepAsync_ClaudeWebWithSession_SavesWithSessionCredential()
    {
        var repo = new FakeProviderRepository();
        var request = new OnboardingProviderSetupApplicationService.Step3Request(
            ProviderName: "Claude.ai",
            ProviderType: "ClaudeWeb",
            ApiKey: null,
            BaseUrl: null,
            StandardModel: null,
            OllamaModel: null,
            ClaudeWebSessionReady: true);

        var result = await _service.CompleteStepAsync(repo, request);

        Assert.True(result.ShouldAdvance);
        Assert.True(result.SavedProvider);
        Assert.Equal("ClaudeWeb", result.ProviderType);
        Assert.Equal("claude.ai-session", repo.InsertedProviders[0].ApiKey);
    }

    [Fact]
    public async Task CompleteStepAsync_ClaudeWebWithoutSession_ReturnsAdvanceWithoutSave()
    {
        var repo = new FakeProviderRepository();
        var request = new OnboardingProviderSetupApplicationService.Step3Request(
            ProviderName: "Claude.ai",
            ProviderType: "ClaudeWeb",
            ApiKey: null,
            BaseUrl: null,
            StandardModel: null,
            OllamaModel: null,
            ClaudeWebSessionReady: false);

        var result = await _service.CompleteStepAsync(repo, request);

        Assert.True(result.ShouldAdvance);
        Assert.False(result.SavedProvider);
    }

    [Fact]
    public async Task CompleteStepAsync_OllamaNullModel_UsesEmptyString()
    {
        var repo = new FakeProviderRepository();
        var request = new OnboardingProviderSetupApplicationService.Step3Request(
            ProviderName: "Ollama No Model",
            ProviderType: "Ollama",
            ApiKey: null,
            BaseUrl: null,
            StandardModel: "gpt-4o",
            OllamaModel: null,
            ClaudeWebSessionReady: false);

        var result = await _service.CompleteStepAsync(repo, request);

        Assert.Equal(string.Empty, result.Model);
    }

    [Fact]
    public async Task CompleteStepAsync_StandardModelNull_UsesEmptyString()
    {
        var repo = new FakeProviderRepository();
        var request = new OnboardingProviderSetupApplicationService.Step3Request(
            ProviderName: "No Model",
            ProviderType: "OpenAI",
            ApiKey: "sk-test",
            BaseUrl: null,
            StandardModel: null,
            OllamaModel: "llama3",
            ClaudeWebSessionReady: false);

        var result = await _service.CompleteStepAsync(repo, request);

        Assert.Equal(string.Empty, result.Model);
    }

    [Fact]
    public async Task CompleteStepAsync_CredentiallessType_SavesWithoutApiKey()
    {
        var repo = new FakeProviderRepository();
        var request = new OnboardingProviderSetupApplicationService.Step3Request(
            ProviderName: "Codex",
            ProviderType: "Codex",
            ApiKey: null,
            BaseUrl: null,
            StandardModel: "codex-1",
            OllamaModel: null,
            ClaudeWebSessionReady: false);

        var result = await _service.CompleteStepAsync(repo, request);

        Assert.True(result.ShouldAdvance);
        Assert.True(result.SavedProvider);
        Assert.Equal("Codex", result.ProviderType);
    }

    [Fact]
    public async Task CompleteStepAsync_ProviderNameIsTrimmed()
    {
        var repo = new FakeProviderRepository();
        var request = new OnboardingProviderSetupApplicationService.Step3Request(
            ProviderName: "  Trimmed Name  ",
            ProviderType: "OpenAI",
            ApiKey: "sk-test",
            BaseUrl: null,
            StandardModel: "gpt-4o",
            OllamaModel: null,
            ClaudeWebSessionReady: false);

        var result = await _service.CompleteStepAsync(repo, request);

        Assert.True(result.SavedProvider);
        Assert.Equal("Trimmed Name", repo.InsertedProviders[0].Name);
    }

    private sealed class FakeProviderRepository : IProviderRepository
    {
        public List<Provider> ExistingProviders { get; } = [];
        public List<Provider> InsertedProviders { get; } = [];

        public Task<List<Provider>> GetProvidersAsync()
            => Task.FromResult(ExistingProviders.ToList());

        public Task<int> InsertProviderAsync(Provider provider)
        {
            provider.Id = InsertedProviders.Count + 1;
            InsertedProviders.Add(provider);
            return Task.FromResult(provider.Id);
        }

        public Task UpdateProviderAsync(Provider provider)
            => Task.CompletedTask;

        public Task SaveProviderOrderAsync(IEnumerable<Provider> orderedProviders)
            => Task.CompletedTask;

        public Task DeleteProviderAsync(int id)
            => Task.CompletedTask;
    }
}
