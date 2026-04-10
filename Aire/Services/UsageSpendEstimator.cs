using System;
using System.Collections.Generic;

namespace Aire.Services
{
    /// <summary>
    /// Estimates token spend from public model pricing when a provider does not expose live billing data.
    /// </summary>
    public static class UsageSpendEstimator
    {
        private sealed record PricingTier(decimal InputUsdPerMillionTokens, decimal OutputUsdPerMillionTokens);

        private static readonly Dictionary<string, Dictionary<string, PricingTier>> PricingByProvider =
            new(StringComparer.OrdinalIgnoreCase)
            {
                ["Codex"] = new Dictionary<string, PricingTier>(StringComparer.OrdinalIgnoreCase)
                {
                    ["gpt-5-codex"] = new(1.25m, 10.00m),
                    ["gpt-5-mini"] = new(0.25m, 2.00m),
                    ["gpt-5.4"] = new(2.50m, 15.00m),
                    ["gpt-5.4-mini"] = new(0.75m, 4.50m),
                },
                ["ClaudeCode"] = new Dictionary<string, PricingTier>(StringComparer.OrdinalIgnoreCase)
                {
                    ["claude-sonnet-4-5"] = new(3.00m, 15.00m),
                    ["claude-3-7-sonnet-latest"] = new(3.00m, 15.00m),
                    ["claude-3-5-haiku-latest"] = new(0.80m, 4.00m),
                },
                ["Zai"] = new Dictionary<string, PricingTier>(StringComparer.OrdinalIgnoreCase)
                {
                    ["glm-5.1"] = new(1.40m, 4.40m),
                    ["glm-5"] = new(1.00m, 3.20m),
                    ["glm-5-turbo"] = new(1.20m, 4.00m),
                    ["glm-4.7"] = new(0.60m, 2.20m),
                    ["glm-4.7-flash"] = new(0.00m, 0.00m),
                    ["glm-4.6"] = new(0.60m, 2.20m),
                    ["glm-4.5"] = new(0.60m, 2.20m),
                    ["glm-4.5-air"] = new(0.20m, 1.10m),
                    ["glm-4.5-flash"] = new(0.00m, 0.00m),
                    ["glm-4.6v"] = new(0.30m, 0.90m),
                },
            };

        /// <summary>
        /// Estimates USD spend for the given provider/model token total using a 50/50 input-output blend.
        /// </summary>
        public static bool TryEstimateUsd(
            string providerType,
            string model,
            long tokensUsed,
            out decimal estimatedUsd)
        {
            estimatedUsd = 0m;

            if (tokensUsed <= 0)
                return false;

            if (!TryGetPricing(providerType, model, out var pricing))
                return false;

            var blendedRate = (pricing.InputUsdPerMillionTokens + pricing.OutputUsdPerMillionTokens) / 2m;
            estimatedUsd = Math.Round(tokensUsed * blendedRate / 1_000_000m, 4, MidpointRounding.AwayFromZero);
            return true;
        }

        private static bool TryGetPricing(string providerType, string model, out PricingTier pricing)
        {
            pricing = default!;

            if (string.IsNullOrWhiteSpace(providerType) ||
                string.IsNullOrWhiteSpace(model) ||
                !PricingByProvider.TryGetValue(providerType.Trim(), out var providerPricing))
            {
                return false;
            }

            return providerPricing.TryGetValue(model.Trim(), out pricing!);
        }
    }
}
