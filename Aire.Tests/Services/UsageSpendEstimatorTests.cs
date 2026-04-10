using Aire.Services;
using Xunit;

namespace Aire.Tests.Services
{
    public class UsageSpendEstimatorTests
    {
        [Theory]
        [InlineData("Codex", "gpt-5.4", 1_000_000, 8.75)]
        [InlineData("ClaudeCode", "claude-3-5-haiku-latest", 1_000_000, 2.40)]
        [InlineData("Anthropic", "claude-sonnet-4-6", 1_000_000, 9.00)]
        [InlineData("Anthropic", "claude-haiku-4-5-20251001-preview", 1_000_000, 3.00)]
        [InlineData("Zai", "glm-4.5-air", 1_000_000, 0.65)]
        public void TryEstimateUsd_ReturnsExpectedBlendedEstimate(
            string providerType,
            string model,
            long tokensUsed,
            decimal expectedUsd)
        {
            Assert.True(UsageSpendEstimator.TryEstimateUsd(providerType, model, tokensUsed, out var estimatedUsd));
            Assert.Equal(expectedUsd, estimatedUsd, 2);
        }

        [Fact]
        public void TryEstimateUsd_ReturnsFalseForUnknownProviderModel()
        {
            Assert.False(UsageSpendEstimator.TryEstimateUsd("Unknown", "model", 1000, out var estimatedUsd));
            Assert.Equal(0m, estimatedUsd);
        }
    }
}
