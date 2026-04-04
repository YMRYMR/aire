using System.Collections.Generic;
using System.Threading.Tasks;
using Aire.AppLayer.Abstractions;
using Aire.AppLayer.Providers;
using Aire.Data;
using Xunit;

namespace Aire.Tests.Services;

public sealed class SettingsProviderListApplicationServiceTests
{
    private sealed class FakeProviderRepository : IProviderRepository
    {
        public List<Provider> Providers { get; } = [];
        public bool ThrowOnUpdate { get; set; }

        public Task<List<Provider>> GetProvidersAsync()
            => Task.FromResult(new List<Provider>(Providers));

        public Task<int> InsertProviderAsync(Provider provider)
            => Task.FromResult(0);

        public Task UpdateProviderAsync(Provider provider)
        {
            if (ThrowOnUpdate)
                throw new System.InvalidOperationException("boom");

            return Task.CompletedTask;
        }

        public Task SaveProviderOrderAsync(IEnumerable<Provider> orderedProviders)
            => Task.CompletedTask;

        public Task DeleteProviderAsync(int providerId)
            => Task.CompletedTask;
    }

    [Fact]
    public async Task ToggleEnabledAsync_TogglesAndPersists()
    {
        var service = new SettingsProviderListApplicationService();
        var repository = new FakeProviderRepository();
        var provider = new Provider { Id = 3, IsEnabled = true };

        var result = await service.ToggleEnabledAsync(repository, provider);

        Assert.False(result.IsEnabled);
        Assert.False(provider.IsEnabled);
    }

    [Fact]
    public async Task ToggleEnabledAsync_RevertsOnFailure()
    {
        var service = new SettingsProviderListApplicationService();
        var repository = new FakeProviderRepository { ThrowOnUpdate = true };
        var provider = new Provider { Id = 3, IsEnabled = true };

        await Assert.ThrowsAsync<System.InvalidOperationException>(() => service.ToggleEnabledAsync(repository, provider));
        Assert.True(provider.IsEnabled);
    }

    [Fact]
    public async Task CreateDefaultProviderAsync_UsesCanonicalProviderDefaults()
    {
        var service = new SettingsProviderListApplicationService();
        var repository = new FakeProviderRepository();

        var provider = await service.CreateDefaultProviderAsync(repository);

        Assert.Equal("OpenAI", provider.Name);
        Assert.Equal("OpenAI", provider.Type);
        Assert.False(string.IsNullOrWhiteSpace(provider.Model));
        Assert.True(provider.IsEnabled);
        Assert.Equal("#888888", provider.Color);
    }

    [Fact]
    public async Task LoadAsync_ReselectsPreferredProvider_OrFallsBackToCurrentSelection()
    {
        var service = new SettingsProviderListApplicationService();
        var repository = new FakeProviderRepository();
        repository.Providers.AddRange(
            [
                new Provider { Id = 4, Name = "OpenAI", Type = "OpenAI" },
                new Provider { Id = 9, Name = "Groq", Type = "Groq" }
            ]);

        var reselection = await service.LoadAsync(repository, reselectId: 9, currentSelectedId: 4);
        var fallback = await service.LoadAsync(repository, reselectId: null, currentSelectedId: 4);
        var none = await service.LoadAsync(repository, reselectId: null, currentSelectedId: null);

        Assert.Equal(9, reselection.SelectedProvider!.Id);
        Assert.Equal(4, fallback.SelectedProvider!.Id);
        Assert.Null(none.SelectedProvider);
    }

    [Fact]
    public void Reorder_ReturnsNoChange_WhenDraggedOrTargetMissing()
    {
        var service = new SettingsProviderListApplicationService();
        var providers = new[]
        {
            new Provider { Id = 1, Name = "OpenAI" },
            new Provider { Id = 2, Name = "Groq" }
        };

        var result = service.Reorder(providers, draggedProviderId: 1, targetProviderId: 99);

        Assert.False(result.OrderChanged);
        Assert.Equal(2, result.Providers.Count);
        Assert.Equal(1, result.SelectedProvider!.Id);
    }
}
