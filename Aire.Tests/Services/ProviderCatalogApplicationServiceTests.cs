using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Aire.AppLayer.Abstractions;
using Aire.AppLayer.Providers;
using Aire.Data;
using Xunit;

namespace Aire.Tests.Services;

public sealed class ProviderCatalogApplicationServiceTests
{
    private sealed class FakeProviderRepository : IProviderRepository
    {
        private readonly List<Provider> _providers = [];

        public FakeProviderRepository AddProvider(Provider provider)
        {
            provider.Id = _providers.Count + 1;
            _providers.Add(provider);
            return this;
        }

        public Task<List<Provider>> GetProvidersAsync()
            => Task.FromResult(new List<Provider>(_providers));

        public Task UpdateProviderAsync(Provider provider) => Task.CompletedTask;
        public Task SaveProviderOrderAsync(IEnumerable<Provider> orderedProviders) => Task.CompletedTask;
        public Task<int> InsertProviderAsync(Provider provider) => Task.FromResult(0);
        public Task DeleteProviderAsync(int id) => Task.CompletedTask;
    }

    private static Provider MakeProvider(string name, bool isEnabled) => new()
    {
        Name = name,
        Type = "OpenAI",
        Model = "gpt-4.1-mini",
        IsEnabled = isEnabled,
        Color = "#888888"
    };

    [Fact]
    public async Task LoadProviderCatalogAsync_MixedEnabledDisabled_ReturnsCorrectLists()
    {
        var repo = new FakeProviderRepository();
        repo.AddProvider(MakeProvider("Alpha", isEnabled: true));
        repo.AddProvider(MakeProvider("Beta", isEnabled: false));
        repo.AddProvider(MakeProvider("Gamma", isEnabled: true));

        var service = new ProviderCatalogApplicationService(repo);
        var result = await service.LoadProviderCatalogAsync(autoSelect: false, savedProviderId: null);

        Assert.Equal(3, result.AllProviders.Count);
        Assert.Equal(2, result.EnabledProviders.Count);
        Assert.Null(result.SelectedProvider);
        Assert.Null(result.EmptyStateMessage);
    }

    [Fact]
    public async Task LoadProviderCatalogAsync_AutoSelect_PicksFirstEnabled()
    {
        var repo = new FakeProviderRepository();
        repo.AddProvider(MakeProvider("Alpha", isEnabled: true));
        repo.AddProvider(MakeProvider("Beta", isEnabled: true));

        var service = new ProviderCatalogApplicationService(repo);
        var result = await service.LoadProviderCatalogAsync(autoSelect: true, savedProviderId: null);

        Assert.NotNull(result.SelectedProvider);
        Assert.Equal("Alpha", result.SelectedProvider!.Name);
    }

    [Fact]
    public async Task LoadProviderCatalogAsync_SavedProviderId_ThatExists_SelectsIt()
    {
        var repo = new FakeProviderRepository();
        repo.AddProvider(MakeProvider("Alpha", isEnabled: true));
        repo.AddProvider(MakeProvider("Beta", isEnabled: true));
        // Beta is id=2
        const int betaId = 2;

        var service = new ProviderCatalogApplicationService(repo);
        var result = await service.LoadProviderCatalogAsync(autoSelect: false, savedProviderId: betaId);

        Assert.NotNull(result.SelectedProvider);
        Assert.Equal(betaId, result.SelectedProvider!.Id);
        Assert.Equal("Beta", result.SelectedProvider.Name);
    }

    [Fact]
    public async Task LoadProviderCatalogAsync_SavedProviderId_ThatDoesNotExist_FallsBack()
    {
        var repo = new FakeProviderRepository();
        repo.AddProvider(MakeProvider("Alpha", isEnabled: true));

        var service = new ProviderCatalogApplicationService(repo);
        var result = await service.LoadProviderCatalogAsync(autoSelect: true, savedProviderId: 999);

        // Saved id 999 doesn't match, autoSelect=true falls back to first enabled
        Assert.NotNull(result.SelectedProvider);
        Assert.Equal("Alpha", result.SelectedProvider!.Name);
    }

    [Fact]
    public async Task LoadProviderCatalogAsync_NoEnabledProviders_ReturnsEmptyStateMessage()
    {
        var repo = new FakeProviderRepository();
        repo.AddProvider(MakeProvider("Alpha", isEnabled: false));
        repo.AddProvider(MakeProvider("Beta", isEnabled: false));

        var service = new ProviderCatalogApplicationService(repo);
        var result = await service.LoadProviderCatalogAsync(autoSelect: true, savedProviderId: null);

        Assert.Empty(result.EnabledProviders);
        Assert.Null(result.SelectedProvider);
        Assert.NotNull(result.EmptyStateMessage);
        Assert.NotEmpty(result.EmptyStateMessage);
    }

    [Fact]
    public async Task LoadProviderCatalogAsync_SavedProviderIdNotFound_AndAutoSelectFalse_ReturnsNull()
    {
        var repo = new FakeProviderRepository();
        repo.AddProvider(MakeProvider("Alpha", isEnabled: true));

        var service = new ProviderCatalogApplicationService(repo);
        var result = await service.LoadProviderCatalogAsync(autoSelect: false, savedProviderId: 999);

        Assert.Null(result.SelectedProvider);
    }

    [Fact]
    public void ResolveSelectionAfterRefresh_MatchingId_ReturnsProvider()
    {
        var repo = new FakeProviderRepository();
        var service = new ProviderCatalogApplicationService(repo);

        var providers = new List<Provider>
        {
            new() { Id = 1, Name = "Alpha" },
            new() { Id = 2, Name = "Beta" }
        };

        var result = service.ResolveSelectionAfterRefresh(providers, selectedProviderId: 2);

        Assert.NotNull(result);
        Assert.Equal(2, result!.Id);
        Assert.Equal("Beta", result.Name);
    }

    [Fact]
    public void ResolveSelectionAfterRefresh_NullId_ReturnsNull()
    {
        var repo = new FakeProviderRepository();
        var service = new ProviderCatalogApplicationService(repo);

        var providers = new List<Provider>
        {
            new() { Id = 1, Name = "Alpha" }
        };

        var result = service.ResolveSelectionAfterRefresh(providers, selectedProviderId: null);

        Assert.Null(result);
    }
}
