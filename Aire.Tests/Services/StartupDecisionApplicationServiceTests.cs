using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Aire.AppLayer.Abstractions;
using Aire.AppLayer.Startup;
using Aire.Data;
using Xunit;

namespace Aire.Tests.Services;

public sealed class StartupDecisionApplicationServiceTests
{
    private sealed class FakeProviderRepository : IProviderRepository
    {
        public List<Provider> Providers { get; } = [];
        public bool ThrowOnRead { get; set; }

        public Task<List<Provider>> GetProvidersAsync()
        {
            if (ThrowOnRead)
            {
                throw new InvalidOperationException("provider lookup failed");
            }

            return Task.FromResult(new List<Provider>(Providers));
        }

        public Task<int> InsertProviderAsync(Provider provider) => throw new NotSupportedException();
        public Task UpdateProviderAsync(Provider provider) => throw new NotSupportedException();
        public Task SaveProviderOrderAsync(IEnumerable<Provider> orderedProviders) => throw new NotSupportedException();
        public Task DeleteProviderAsync(int id) => throw new NotSupportedException();
    }

    [Fact]
    public async Task ShouldShowOnboardingAsync_ReturnsTrue_WhenOnboardingWasNotCompleted()
    {
        var service = new StartupDecisionApplicationService();
        var repository = new FakeProviderRepository();

        bool result = await service.ShouldShowOnboardingAsync(repository, onboardingCompleted: false);

        Assert.True(result);
    }

    [Fact]
    public async Task ShouldShowOnboardingAsync_ReturnsTrue_WhenNoProvidersExist()
    {
        var service = new StartupDecisionApplicationService();
        var repository = new FakeProviderRepository();

        bool result = await service.ShouldShowOnboardingAsync(repository, onboardingCompleted: true);

        Assert.True(result);
    }

    [Fact]
    public async Task ShouldShowOnboardingAsync_ReturnsFalse_WhenProvidersExist()
    {
        var service = new StartupDecisionApplicationService();
        var repository = new FakeProviderRepository();
        repository.Providers.Add(new Provider { Id = 1, Type = "OpenAI", Model = "gpt-5.4-mini" });

        bool result = await service.ShouldShowOnboardingAsync(repository, onboardingCompleted: true);

        Assert.False(result);
    }

    [Fact]
    public async Task ShouldShowOnboardingAsync_ReturnsFalse_WhenProviderLookupFails()
    {
        var service = new StartupDecisionApplicationService();
        var repository = new FakeProviderRepository
        {
            ThrowOnRead = true
        };

        bool result = await service.ShouldShowOnboardingAsync(repository, onboardingCompleted: true);

        Assert.False(result);
    }
}
