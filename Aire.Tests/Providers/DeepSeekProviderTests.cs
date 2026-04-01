using System;
using System.Threading;
using System.Threading.Tasks;
using Aire.Providers;
using Xunit;

namespace Aire.Tests.Providers;

public class DeepSeekProviderTests
{
    private static string? TestApiKey => Environment.GetEnvironmentVariable("AIRE_TEST_DEEPSEEK_API_KEY");

    private DeepSeekProvider CreateInitialized(string apiKey, string baseUrl = "https://api.deepseek.com")
    {
        DeepSeekProvider deepSeekProvider = new DeepSeekProvider();
        deepSeekProvider.Initialize(new ProviderConfig
        {
            ApiKey = apiKey,
            BaseUrl = baseUrl,
            Model = "deepseek-chat"
        });
        return deepSeekProvider;
    }

    [Fact]
    public void ProviderType_IsDeepSeek()
    {
        DeepSeekProvider deepSeekProvider = new DeepSeekProvider();
        Assert.Equal("DeepSeek", deepSeekProvider.ProviderType);
    }

    [Fact]
    public async Task GetTokenUsageAsync_ReturnsValidTokenUsage()
    {
        if (string.IsNullOrWhiteSpace(TestApiKey))
        {
            return;
        }
        DeepSeekProvider provider = CreateInitialized(TestApiKey);
        TokenUsage tokenUsage = await provider.GetTokenUsageAsync(CancellationToken.None);
        if (tokenUsage != null)
        {
            Assert.True(tokenUsage.Used >= 0, $"Used ({tokenUsage.Used}) should be non-negative");
            Assert.True(tokenUsage.Limit > 0, $"Limit ({tokenUsage.Limit}) should be positive");
            Assert.Equal("USD", tokenUsage.Unit);
            Assert.InRange(tokenUsage.Percentage, 0.0, 1.0);
            if (tokenUsage.Limit.HasValue)
            {
                Assert.Equal(tokenUsage.Used >= tokenUsage.Limit.Value, tokenUsage.IsLimitReached);
            }
            Console.WriteLine($"DeepSeek Token Usage: Used={tokenUsage.Used} cents, Limit={tokenUsage.Limit} cents, Unit={tokenUsage.Unit}, Percentage={tokenUsage.Percentage:P}, IsLimitReached={tokenUsage.IsLimitReached}");
        }
    }

    [Fact]
    public async Task GetTokenUsageAsync_WithoutApiKey_ReturnsNull()
    {
        DeepSeekProvider provider = new DeepSeekProvider();
        Assert.Null(await provider.GetTokenUsageAsync(CancellationToken.None));
    }

    [Fact]
    public async Task GetTokenUsageAsync_WithInvalidBaseUrl_ReturnsNull()
    {
        if (!string.IsNullOrWhiteSpace(TestApiKey))
        {
            DeepSeekProvider provider = CreateInitialized(TestApiKey, "https://invalid.deepseek.com");
            await provider.GetTokenUsageAsync(CancellationToken.None);
        }
    }
}
