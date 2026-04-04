using System.Collections.Generic;
using System.Threading.Tasks;
using Aire.AppLayer.Abstractions;
using Aire.AppLayer.Providers;
using Aire.Data;
using Xunit;

namespace Aire.Tests.Services;

public sealed class ProviderCreationApplicationServiceTests
{
    private sealed class FakeProviderRepository : IProviderRepository
    {
        public List<Provider> Providers { get; } = [];
        public int NextId { get; set; } = 1;
        public List<Provider> InsertedProviders { get; } = [];

        public Task<List<Provider>> GetProvidersAsync()
            => Task.FromResult(new List<Provider>(Providers));

        public Task<int> InsertProviderAsync(Provider provider)
        {
            provider.Id = NextId++;
            InsertedProviders.Add(Clone(provider));
            Providers.Add(Clone(provider));
            return Task.FromResult(provider.Id);
        }

        public Task UpdateProviderAsync(Provider provider) => Task.CompletedTask;
        public Task SaveProviderOrderAsync(IEnumerable<Provider> orderedProviders) => Task.CompletedTask;
        public Task DeleteProviderAsync(int id) => Task.CompletedTask;

        private static Provider Clone(Provider provider)
            => new()
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
            };
    }

    [Fact]
    public async Task CreateAsync_ReturnsDuplicateResult_WithoutInserting()
    {
        var repository = new FakeProviderRepository
        {
        };
        repository.Providers.Add(new Provider { Id = 7, Name = "OpenAI", Type = "OpenAI", Model = "gpt-5.4-mini" });
        var service = new ProviderCreationApplicationService();

        var result = await service.CreateAsync(repository, new ProviderCreationApplicationService.ProviderCreationRequest(
            "OpenAI Copy",
            "OpenAI",
            "sk-test",
            "https://example.test",
            "gpt-5.4-mini",
            true,
            null));

        Assert.True(result.IsDuplicate);
        Assert.Empty(repository.InsertedProviders);
    }

    [Fact]
    public async Task CreateAsync_InheritsCredentials_WhenRequested()
    {
        var repository = new FakeProviderRepository
        {
        };
        repository.Providers.Add(new Provider
        {
            Id = 3,
            Name = "Source",
            Type = "OpenAI",
            ApiKey = "source-key",
            BaseUrl = "https://source.example",
            Model = "gpt-5.4-mini"
        });
        var service = new ProviderCreationApplicationService();

        var result = await service.CreateAsync(repository, new ProviderCreationApplicationService.ProviderCreationRequest(
            null,
            "Groq",
            null,
            null,
            "llama-3.3-70b",
            true,
            null,
            InheritCredentialsFromProviderId: 3));

        Assert.False(result.IsDuplicate);
        Assert.Equal("Groq", result.Provider.Type);
        Assert.Equal("source-key", result.Provider.ApiKey);
        Assert.Equal("https://source.example", result.Provider.BaseUrl);
        Assert.Single(repository.InsertedProviders);
    }

    [Fact]
    public async Task CreateAsync_UsesDescriptorDefaults_WhenNameMissing()
    {
        var repository = new FakeProviderRepository();
        var service = new ProviderCreationApplicationService();

        var result = await service.CreateAsync(repository, new ProviderCreationApplicationService.ProviderCreationRequest(
            null,
            "OpenAI",
            "sk-test",
            null,
            "gpt-5.4-mini",
            true,
            null));

        Assert.False(result.IsDuplicate);
        Assert.Equal("OpenAI", result.Provider.Name);
        Assert.Equal("OpenAI", result.Provider.Type);
        Assert.Single(repository.InsertedProviders);
    }
}
