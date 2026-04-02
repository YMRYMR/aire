using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Aire.AppLayer.Abstractions;
using Aire.Data;
using Aire.Domain.Providers;
using Aire.Providers;
using Aire.Services.Providers;
using Xunit;

namespace Aire.Tests.Services;

public sealed class ProviderConfigurationWorkflowServiceTests
{
    [Fact]
    public void CreateRuntimeProvider_RequiresApiKey_ForRemoteProviders()
    {
        var service = new ProviderConfigurationWorkflowService();

        var provider = service.CreateRuntimeProvider(new ProviderRuntimeRequest(
            "OpenAI",
            " ",
            null,
            "gpt-5.4-mini",
            ClaudeWebSessionReady: false));

        Assert.Null(provider);
    }

    [Fact]
    public void CreateRuntimeProvider_RequiresClaudeSession_ForClaudeWeb()
    {
        var service = new ProviderConfigurationWorkflowService();

        var provider = service.CreateRuntimeProvider(new ProviderRuntimeRequest(
            "ClaudeWeb",
            null,
            null,
            "claude-sonnet",
            ClaudeWebSessionReady: false));

        Assert.Null(provider);
    }

    [Fact]
    public void CreateRuntimeProvider_CreatesCodexWithoutApiKey()
    {
        var service = new ProviderConfigurationWorkflowService();

        var provider = service.CreateRuntimeProvider(new ProviderRuntimeRequest(
            "Codex",
            null,
            null,
            "gpt-5.4-mini",
            ClaudeWebSessionReady: false));

        Assert.NotNull(provider);
        Assert.IsType<CodexProvider>(provider);
    }

    [Fact]
    public void CreateRuntimeProvider_CreatesClaudeWeb_WithSyntheticSessionCredential()
    {
        var service = new ProviderConfigurationWorkflowService();

        var provider = service.CreateRuntimeProvider(new ProviderRuntimeRequest(
            "ClaudeWeb",
            null,
            null,
            "claude-sonnet",
            ClaudeWebSessionReady: true));

        Assert.NotNull(provider);
        Assert.Equal("ClaudeWeb", provider!.ProviderType);
    }

    [Fact]
    public void CreateRuntimeProvider_DefaultsUnknownTypeToOpenAi()
    {
        var service = new ProviderConfigurationWorkflowService();

        var provider = service.CreateRuntimeProvider(new ProviderRuntimeRequest(
            "UnknownType",
            "sk-test",
            " https://example.test/ ",
            "gpt-4.1-mini",
            ClaudeWebSessionReady: false));

        Assert.NotNull(provider);
        Assert.IsType<OpenAiProvider>(provider);
    }

    [Fact]
    public async Task SaveNewProviderAsync_BlocksDuplicates_AndReturnsInsertedId()
    {
        var service = new ProviderConfigurationWorkflowService();
        var repository = new RecordingProviderRepository
        {
            ExistingProviders =
            [
                new Provider { Id = 3, Type = "OpenAI", Model = "gpt-4.1-mini" }
            ],
            NextInsertedId = 11
        };

        var duplicate = await service.SaveNewProviderAsync(repository, new ProviderDraft(
            "OpenAI Main",
            "OpenAI",
            "sk-test",
            null,
            "gpt-4.1-mini"));

        var saved = await service.SaveNewProviderAsync(repository, new ProviderDraft(
            "Groq Main",
            "Groq",
            "groq-key",
            null,
            "llama-3.3-70b"));

        Assert.False(duplicate.Saved);
        Assert.True(duplicate.IsDuplicate);
        Assert.True(saved.Saved);
        Assert.False(saved.IsDuplicate);
        Assert.Equal(11, saved.ProviderId);
        Assert.Single(repository.InsertedProviders);
        Assert.Equal("Groq", repository.InsertedProviders[0].Type);
    }

    [Fact]
    public void ApplyProviderEditorValues_NormalizesBasicFields()
    {
        var service = new ProviderConfigurationWorkflowService();
        var provider = new Provider();

        service.ApplyProviderEditorValues(
            provider,
            "  My Provider  ",
            "",
            "api-key",
            " https://example.test/ ",
            "gpt-4.1-mini",
            null,
            timeoutMinutes: 7,
            isEnabled: true);

        Assert.Equal("My Provider", provider.Name);
        Assert.Equal("OpenAI", provider.Type);
        Assert.Equal("api-key", provider.ApiKey);
        Assert.Equal("https://example.test/", provider.BaseUrl);
        Assert.Equal("gpt-4.1-mini", provider.Model);
        Assert.Equal(7, provider.TimeoutMinutes);
        Assert.True(provider.IsEnabled);
    }

    [Fact]
    public void NormalizeModelSelection_PrefersSelectedValue_ThenDecoratedMapping()
    {
        var service = new ProviderConfigurationWorkflowService();
        var mappings = new (string DisplayName, string ModelName)[]
        {
            ("✓ qwen3:4b  (3.2 GB)", "qwen3:4b"),
            ("qwen3:8b", "qwen3:8b")
        };

        Assert.Equal("selected-model", service.NormalizeModelSelection("ignored", "selected-model", mappings));
        Assert.Equal("qwen3:4b", service.NormalizeModelSelection("✓ qwen3:4b  (3.2 GB)", null, mappings));
        Assert.Equal("qwen3:8b", service.NormalizeModelSelection("qwen3:8b", null, mappings));
        Assert.Equal("custom-model", service.NormalizeModelSelection("custom-model", null, mappings));
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
        public Task DeleteProviderAsync(int providerId) => throw new NotSupportedException();
    }
}
