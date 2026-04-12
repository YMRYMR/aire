using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Aire.AppLayer.Abstractions;
using Aire.AppLayer.Providers;
using Aire.Data;
using Xunit;

namespace Aire.Tests.Services;

public sealed class ProviderEditorSaveApplicationServiceTests
{
    private sealed class FakeProviderRepository : IProviderRepository
    {
        public List<Provider> UpdatedProviders { get; } = [];

        public Task<List<Provider>> GetProvidersAsync()
            => Task.FromResult(new List<Provider>());

        public Task UpdateProviderAsync(Provider provider)
        {
            UpdatedProviders.Add(new Provider
            {
                Id = provider.Id,
                Name = provider.Name,
                Type = provider.Type,
                ApiKey = provider.ApiKey,
                BaseUrl = provider.BaseUrl,
                Model = provider.Model,
                IsEnabled = provider.IsEnabled,
                Color = provider.Color,
                SortOrder = provider.SortOrder,
                TimeoutMinutes = provider.TimeoutMinutes
            });
            return Task.CompletedTask;
        }

        public Task SaveProviderOrderAsync(IEnumerable<Provider> orderedProviders) => Task.CompletedTask;
        public Task<int> InsertProviderAsync(Provider provider) => Task.FromResult(0);
        public Task DeleteProviderAsync(int id) => Task.CompletedTask;
    }

    [Fact]
    public async Task SaveAsync_UpdatesProviderWithEditorValues()
    {
        var repository = new FakeProviderRepository();
        var service = new ProviderEditorSaveApplicationService();
        var provider = new Provider
        {
            Id = 5,
            Name = "Old Name",
            Type = "OpenAI",
            ApiKey = "old-key",
            BaseUrl = "https://old.example",
            Model = "gpt-4",
            TimeoutMinutes = 3,
            IsEnabled = true
        };

        var request = new ProviderEditorSaveApplicationService.SaveRequest(
            Provider: provider,
            Name: "  Updated Name  ",
            Type: "Anthropic",
            ApiKey: "new-key",
            BaseUrl: "https://new.example",
            RawModelText: "claude-sonnet-4-20250514",
            SelectedModelValue: null,
            TimeoutMinutes: 10,
            IsEnabled: false,
            KnownModelMappings: null);

        await service.SaveAsync(request, repository);

        // Verify the provider object was mutated correctly
        Assert.Equal("Updated Name", provider.Name);
        Assert.Equal("Anthropic", provider.Type);
        Assert.Equal("new-key", provider.ApiKey);
        Assert.Equal("https://new.example", provider.BaseUrl);
        Assert.Equal("claude-sonnet-4-20250514", provider.Model);
        Assert.Equal(10, provider.TimeoutMinutes);
        Assert.False(provider.IsEnabled);
    }

    [Fact]
    public async Task SaveAsync_PassesUpdatedProviderToRepository()
    {
        var repository = new FakeProviderRepository();
        var service = new ProviderEditorSaveApplicationService();
        var provider = new Provider
        {
            Id = 12,
            Name = "Test Provider",
            Type = "OpenAI",
            ApiKey = "sk-abc",
            BaseUrl = "",
            Model = "gpt-4",
            TimeoutMinutes = Provider.DefaultTimeoutMinutes,
            IsEnabled = true
        };

        var request = new ProviderEditorSaveApplicationService.SaveRequest(
            Provider: provider,
            Name: "Renamed",
            Type: "OpenAI",
            ApiKey: "sk-abc",
            BaseUrl: null,
            RawModelText: "gpt-5.4-mini",
            SelectedModelValue: null,
            TimeoutMinutes: 7,
            IsEnabled: true,
            KnownModelMappings: null);

        await service.SaveAsync(request, repository);

        Assert.Single(repository.UpdatedProviders);
        var persisted = repository.UpdatedProviders[0];
        Assert.Equal(12, persisted.Id);
        Assert.Equal("Renamed", persisted.Name);
        Assert.Equal("gpt-5.4-mini", persisted.Model);
        Assert.Equal(7, persisted.TimeoutMinutes);
    }

    [Fact]
    public async Task SaveAsync_NormalizesModelViaSelectedModelValue()
    {
        var repository = new FakeProviderRepository();
        var service = new ProviderEditorSaveApplicationService();
        var provider = new Provider
        {
            Id = 1,
            Name = "Ollama",
            Type = "Ollama",
            Model = "old-model"
        };

        var request = new ProviderEditorSaveApplicationService.SaveRequest(
            Provider: provider,
            Name: "Ollama",
            Type: "Ollama",
            ApiKey: null,
            BaseUrl: "http://localhost:11434",
            RawModelText: "Qwen3:4B  (2.3 GB)",
            SelectedModelValue: "qwen3:4b",
            TimeoutMinutes: 5,
            IsEnabled: true,
            KnownModelMappings: null);

        await service.SaveAsync(request, repository);

        // selectedModelValue takes priority over rawModelText
        Assert.Equal("qwen3:4b", provider.Model);
    }

    [Fact]
    public async Task SaveAsync_ResolvesDecoratedModelTextViaKnownModelMappings()
    {
        var repository = new FakeProviderRepository();
        var service = new ProviderEditorSaveApplicationService();
        var provider = new Provider
        {
            Id = 2,
            Name = "Ollama Local",
            Type = "Ollama",
            Model = "old"
        };

        var mappings = new (string DisplayName, string ModelName)[]
        {
            ("llama3.2:3b  (1.2 GB)", "llama3.2:3b"),
            ("qwen3:4b  (2.3 GB)", "qwen3:4b")
        };

        var request = new ProviderEditorSaveApplicationService.SaveRequest(
            Provider: provider,
            Name: "Ollama Local",
            Type: "Ollama",
            ApiKey: null,
            BaseUrl: "http://localhost:11434",
            RawModelText: "qwen3:4b  (2.3 GB)",
            SelectedModelValue: null,
            TimeoutMinutes: 5,
            IsEnabled: true,
            KnownModelMappings: mappings);

        await service.SaveAsync(request, repository);

        Assert.Equal("qwen3:4b", provider.Model);
    }

    [Fact]
    public async Task SaveAsync_DefaultsEmptyTypeToOpenAI()
    {
        var repository = new FakeProviderRepository();
        var service = new ProviderEditorSaveApplicationService();
        var provider = new Provider
        {
            Id = 3,
            Name = "Test",
            Type = "Ollama",
            Model = "model"
        };

        var request = new ProviderEditorSaveApplicationService.SaveRequest(
            Provider: provider,
            Name: "Test",
            Type: "",
            ApiKey: "key",
            BaseUrl: null,
            RawModelText: "model",
            SelectedModelValue: null,
            TimeoutMinutes: 5,
            IsEnabled: true,
            KnownModelMappings: null);

        await service.SaveAsync(request, repository);

        Assert.Equal("OpenAI", provider.Type);
    }

    [Fact]
    public async Task SaveAsync_ClearsBaseUrl_WhenWhitespace()
    {
        var repository = new FakeProviderRepository();
        var service = new ProviderEditorSaveApplicationService();
        var provider = new Provider
        {
            Id = 4,
            Name = "Test",
            Type = "OpenAI",
            BaseUrl = "https://old.example",
            Model = "model"
        };

        var request = new ProviderEditorSaveApplicationService.SaveRequest(
            Provider: provider,
            Name: "Test",
            Type: "OpenAI",
            ApiKey: "key",
            BaseUrl: "   ",
            RawModelText: "model",
            SelectedModelValue: null,
            TimeoutMinutes: 5,
            IsEnabled: true,
            KnownModelMappings: null);

        await service.SaveAsync(request, repository);

        Assert.Equal(string.Empty, provider.BaseUrl);
    }

    [Fact]
    public async Task SaveAsync_PreservesProviderIdInRepository()
    {
        var repository = new FakeProviderRepository();
        var service = new ProviderEditorSaveApplicationService();
        var provider = new Provider
        {
            Id = 42,
            Name = "Original",
            Type = "OpenAI",
            ApiKey = "sk-key",
            Model = "gpt-4"
        };

        var request = new ProviderEditorSaveApplicationService.SaveRequest(
            Provider: provider,
            Name: "Updated",
            Type: "OpenAI",
            ApiKey: "sk-key",
            BaseUrl: null,
            RawModelText: "gpt-5.4-mini",
            SelectedModelValue: null,
            TimeoutMinutes: 5,
            IsEnabled: true,
            KnownModelMappings: null);

        await service.SaveAsync(request, repository);

        Assert.Equal(42, provider.Id);
        Assert.Equal(42, repository.UpdatedProviders[0].Id);
    }
}
