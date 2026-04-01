using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Aire.Providers;
using Aire.Services;
using Xunit;

namespace Aire.Tests.Services;

public class TokenUsageServiceTests
{
    private sealed class FakeUsageProvider : BaseAiProvider
    {
        private readonly Func<Task<TokenUsage?>> _usageFactory;

        public int CallCount { get; private set; }

        public override string ProviderType => "FakeUsage";

        public override string DisplayName => "Fake Usage";

        public FakeUsageProvider(Func<Task<TokenUsage?>> usageFactory)
        {
            _usageFactory = usageFactory;
        }

        protected override ProviderCapabilities GetBaseCapabilities()
        {
            return ProviderCapabilities.TextChat;
        }

        public override Task<AiResponse> SendChatAsync(IEnumerable<ChatMessage> messages, CancellationToken cancellationToken = default(CancellationToken))
        {
            return Task.FromResult(new AiResponse
            {
                IsSuccess = true,
                Content = "ok"
            });
        }

        public override async Task<TokenUsage?> GetTokenUsageAsync(CancellationToken ct)
        {
            CallCount++;
            return await _usageFactory();
        }
    }

    [Fact]
    public async Task GetTokenUsageAsync_CachesSuccessfulResults()
    {
        TokenUsageService.ClearCache();
        FakeUsageProvider provider = new FakeUsageProvider(() => Task.FromResult(new TokenUsage
        {
            Used = 5L,
            Limit = 10L
        }));
        TokenUsage first = await TokenUsageService.GetTokenUsageAsync(provider);
        TokenUsage second = await TokenUsageService.GetTokenUsageAsync(provider);
        Assert.NotNull(first);
        Assert.Same(first, second);
        Assert.Equal(1, provider.CallCount);
    }

    [Fact]
    public async Task GetTokenUsageAsync_ForceRefresh_BypassesCache()
    {
        TokenUsageService.ClearCache();
        FakeUsageProvider provider = new FakeUsageProvider(() => Task.FromResult(new TokenUsage
        {
            Used = 1L,
            Limit = 10L
        }));
        await TokenUsageService.GetTokenUsageAsync(provider);
        await TokenUsageService.GetTokenUsageAsync(provider, forceRefresh: true);
        Assert.Equal(2, provider.CallCount);
    }

    [Fact]
    public async Task GetTokenUsageAsync_WhenProviderThrows_ReturnsNullAndCachesFailure()
    {
        TokenUsageService.ClearCache();
        FakeUsageProvider provider = new FakeUsageProvider(delegate
        {
            throw new InvalidOperationException("boom");
        });
        TokenUsage first = await TokenUsageService.GetTokenUsageAsync(provider);
        TokenUsage second = await TokenUsageService.GetTokenUsageAsync(provider);
        Assert.Null(first);
        Assert.Null(second);
        Assert.Equal(1, provider.CallCount);
    }

    [Fact]
    public async Task ClearCache_RemovesCachedEntry()
    {
        TokenUsageService.ClearCache();
        FakeUsageProvider provider = new FakeUsageProvider(() => Task.FromResult(new TokenUsage
        {
            Used = 2L,
            Limit = 10L
        }));
        await TokenUsageService.GetTokenUsageAsync(provider);
        TokenUsageService.ClearCache();
        await TokenUsageService.GetTokenUsageAsync(provider);
        Assert.Equal(2, provider.CallCount);
    }
}
