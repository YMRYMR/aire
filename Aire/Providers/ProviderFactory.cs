using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Aire.AppLayer.Abstractions;
using Aire.Data;

namespace Aire.Providers
{
    /// <summary>
    /// Creates and manages AI provider instances.
    /// </summary>
    public class ProviderFactory
    {
        private readonly IProviderRepository _providerRepository;
        private readonly Dictionary<string, IAiProvider> _providerCache = new();

        /// <summary>
        /// Creates the factory used by the WPF app to materialize configured providers on demand.
        /// </summary>
        /// <param name="providerRepository">Repository used to load persisted provider definitions.</param>
        public ProviderFactory(IProviderRepository providerRepository)
        {
            _providerRepository = providerRepository ?? throw new ArgumentNullException(nameof(providerRepository));
        }

        /// <summary>
        /// Loads all configured providers from persistence.
        /// </summary>
        /// <returns>Provider rows as stored in the application database.</returns>
        public async Task<List<Provider>> GetConfiguredProvidersAsync()
            => await _providerRepository.GetProvidersAsync();

        /// <summary>
        /// Creates or reuses a runtime provider instance for one stored provider configuration.
        /// WPF-only providers are constructed here; core providers are delegated to <see cref="ProviderRegistry"/>.
        /// </summary>
        /// <param name="providerConfig">Persisted provider configuration to activate.</param>
        /// <returns>A cached and initialized provider instance.</returns>
        public IAiProvider CreateProvider(Provider providerConfig)
        {
            if (providerConfig == null)
                throw new ArgumentNullException(nameof(providerConfig));

            var cacheKey = $"{providerConfig.Type}_{providerConfig.Id}";
            if (_providerCache.TryGetValue(cacheKey, out var cached))
                return cached;

            IAiProvider provider = ProviderCatalog.CreateRuntimeProvider(providerConfig.Type);
            provider.Initialize(ProviderRegistry.BuildProviderConfig(providerConfig));

            _providerCache[cacheKey] = provider;
            return provider;
        }

        /// <summary>
        /// Returns the currently active provider, or the first enabled provider when no explicit id is supplied.
        /// </summary>
        /// <param name="providerId">Optional persisted provider id to resolve.</param>
        /// <returns>The initialized provider instance, or <see langword="null"/> when no enabled provider exists.</returns>
        public async Task<IAiProvider?> GetCurrentProviderAsync(int? providerId = null)
        {
            var providers = await GetConfiguredProvidersAsync();
            var target = providerId.HasValue
                ? providers.FirstOrDefault(p => p.Id == providerId.Value && p.IsEnabled)
                : providers.FirstOrDefault(p => p.IsEnabled);
            return target != null ? CreateProvider(target) : null;
        }

        /// <summary>
        /// Clears cached provider instances so future lookups rebuild them from persisted configuration.
        /// </summary>
        public void ClearCache() => _providerCache.Clear();

        /// <summary>
        /// Persists edits to a provider configuration and clears the cache entry so the next
        /// call to <see cref="CreateProvider"/> picks up the updated values.
        /// </summary>
        public async Task UpdateProviderAsync(Provider provider)
        {
            await _providerRepository.UpdateProviderAsync(provider);
            var cacheKey = $"{provider.Type}_{provider.Id}";
            _providerCache.Remove(cacheKey);
        }

        /// <summary>
        /// Returns provider metadata without creating a chat-session binding for a specific persisted provider row.
        /// </summary>
        /// <param name="providerType">Provider type identifier, such as OpenAI or Ollama.</param>
        /// <returns>A metadata implementation for the requested provider type.</returns>
        public static IProviderMetadata GetMetadata(string providerType)
            => ProviderCatalog.CreateMetadataProvider(providerType);
    }
}
