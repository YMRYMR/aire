using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Aire.Providers;

namespace Aire.Services
{
    /// <summary>
    /// Fetches and caches token usage for AI providers.
    /// </summary>
    public static class TokenUsageService
    {
        private static readonly ConcurrentDictionary<string, CacheEntry> Cache = new();

        private class CacheEntry
        {
            public TokenUsage? Usage { get; set; }
            public DateTimeOffset ExpiresAt { get; set; }
        }

        /// <summary>
        /// Gets token usage for the given provider, using cached data if still fresh.
        /// </summary>
        /// <param name="provider">The provider instance (must be initialized with config).</param>
        /// <param name="forceRefresh">If true, ignore cache and fetch anew.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The token usage, or null if the provider does not support quota tracking or the fetch fails.</returns>
        public static async Task<TokenUsage?> GetTokenUsageAsync(
            IAiProvider provider,
            bool forceRefresh = false,
            CancellationToken ct = default)
        {
            if (provider == null)
                return null;

            // Build a cache key that identifies the provider + its configuration.
            // For simplicity we use provider type + API key hash.
            string cacheKey = $"{provider.ProviderType}:{provider.GetHashCode()}";

            if (!forceRefresh && Cache.TryGetValue(cacheKey, out var entry) && entry.ExpiresAt > DateTimeOffset.UtcNow)
                return entry.Usage;

            TokenUsage? usage = null;
            try
            {
                usage = await provider.GetTokenUsageAsync(ct).ConfigureAwait(false);
            }
            catch
            {
                // If the provider throws, we treat it as unsupported and cache null for a short time.
                usage = null;
            }

            // Cache for 5 minutes (or 1 minute for null results to allow retry sooner).
            var expires = DateTimeOffset.UtcNow.AddMinutes(usage == null ? 1 : 5);
            Cache[cacheKey] = new CacheEntry { Usage = usage, ExpiresAt = expires };

            return usage;
        }

        /// <summary>
        /// Clears the cache for a specific provider (or all caches if key is null).
        /// </summary>
        public static void ClearCache(string? cacheKey = null)
        {
            if (cacheKey == null)
                Cache.Clear();
            else
                Cache.TryRemove(cacheKey, out _);
        }
    }
}
