using System.Collections.Generic;
using System.Threading.Tasks;
using Aire.AppLayer.Abstractions;
using Aire.AppLayer.Api;
using Aire.Data;
using Xunit;

namespace Aire.Tests.Services;

public sealed class LocalApiProviderMutationApplicationServiceTests
{
    private sealed class FakeProviderRepository : IProviderRepository
    {
        public List<Provider> Providers { get; } = [];
        public int NextId { get; set; } = 1;

        public Task<List<Provider>> GetProvidersAsync()
            => Task.FromResult(new List<Provider>(Providers));

        public Task<int> InsertProviderAsync(Provider provider)
        {
            provider.Id = NextId++;
            Providers.Add(Clone(provider));
            return Task.FromResult(provider.Id);
        }

        public Task UpdateProviderAsync(Provider provider)
        {
            var index = Providers.FindIndex(p => p.Id == provider.Id);
            if (index >= 0)
                Providers[index] = Clone(provider);

            return Task.CompletedTask;
        }

        public Task SaveProviderOrderAsync(IEnumerable<Provider> orderedProviders)
            => Task.CompletedTask;

        public Task DeleteProviderAsync(int providerId)
            => Task.CompletedTask;

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
    public async Task CreateProviderAsync_ReturnsRefreshAndSelectionPlan()
    {
        var repository = new FakeProviderRepository();
        var service = new LocalApiProviderMutationApplicationService();

        var result = await service.CreateProviderAsync(
            repository,
            new Aire.AppLayer.Providers.ProviderCreationApplicationService.ProviderCreationRequest(
                "Google AI Images",
                "GoogleAIImage",
                "google-key",
                null,
                "gemini-2.5-flash-image",
                true,
                null),
            selectAfterCreate: true);

        Assert.False(result.IsDuplicate);
        Assert.True(result.RefreshProviderCatalog);
        Assert.True(result.RefreshSettingsProviderList);
        Assert.Equal(result.Provider.Id, result.ReselectProviderId);
        Assert.Equal(result.Provider.Id, result.SelectProviderId);
    }

    [Fact]
    public async Task CreateProviderAsync_ReturnsDuplicateFlow_WithoutCatalogRefresh()
    {
        var repository = new FakeProviderRepository();
        repository.Providers.Add(new Provider { Id = 2, Name = "OpenAI", Type = "OpenAI", Model = "gpt-4o" });
        var service = new LocalApiProviderMutationApplicationService();

        var result = await service.CreateProviderAsync(
            repository,
            new Aire.AppLayer.Providers.ProviderCreationApplicationService.ProviderCreationRequest(
                "OpenAI Copy",
                "OpenAI",
                "google-key",
                null,
                "gpt-4o",
                true,
                null),
            selectAfterCreate: true);

        Assert.True(result.IsDuplicate);
        Assert.False(result.RefreshProviderCatalog);
        Assert.False(result.RefreshSettingsProviderList);
        Assert.Null(result.ReselectProviderId);
        Assert.Null(result.SelectProviderId);
    }

    [Fact]
    public async Task UpdateProviderModelAsync_RefreshesActiveProviderWithoutCatalogReload()
    {
        var repository = new FakeProviderRepository();
        repository.Providers.Add(new Provider { Id = 9, Name = "OpenAI", Type = "OpenAI", Model = "gpt-4o" });
        var service = new LocalApiProviderMutationApplicationService();

        var result = await service.UpdateProviderModelAsync(repository, 9, "gpt-5.4-mini", activeProviderId: 9);

        Assert.True(result.Updated);
        Assert.True(result.RefreshActiveProvider);
        Assert.False(result.RefreshProviderCatalog);
        Assert.True(result.RefreshSettingsProviderList);
        Assert.Equal("gpt-5.4-mini", result.Provider!.Model);
    }

    [Fact]
    public async Task UpdateProviderModelAsync_RefreshesCatalogForInactiveProvider()
    {
        var repository = new FakeProviderRepository();
        repository.Providers.Add(new Provider { Id = 12, Name = "OpenAI", Type = "OpenAI", Model = "gpt-4o" });
        var service = new LocalApiProviderMutationApplicationService();

        var result = await service.UpdateProviderModelAsync(repository, 12, "gpt-5.4-mini", activeProviderId: 7);

        Assert.True(result.Updated);
        Assert.False(result.RefreshActiveProvider);
        Assert.True(result.RefreshProviderCatalog);
        Assert.True(result.RefreshSettingsProviderList);
        Assert.Equal("gpt-5.4-mini", result.Provider!.Model);
    }

    [Fact]
    public async Task UpdateProviderModelAsync_ReturnsNoop_WhenProviderMissing()
    {
        var repository = new FakeProviderRepository();
        var service = new LocalApiProviderMutationApplicationService();

        var result = await service.UpdateProviderModelAsync(repository, 99, "gpt-5.4-mini", activeProviderId: 1);

        Assert.False(result.Updated);
        Assert.Null(result.Provider);
        Assert.False(result.RefreshActiveProvider);
        Assert.False(result.RefreshProviderCatalog);
        Assert.False(result.RefreshSettingsProviderList);
    }
}
