using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Aire.Data;
using Aire.Providers;

namespace Aire.Services
{
    /// <summary>
    /// Fetches the latest available model list directly from each provider's API.
    /// Falls back to the local ModelCatalog files when the network call fails.
    /// </summary>
    public static class LiveModelFetcher
    {
        /// <summary>
        /// Tries to fetch live models for the given provider type and credentials.
        /// Returns <c>null</c> if the fetch fails or the provider doesn't expose a model list.
        /// </summary>
        public static async Task<List<ModelDefinition>?> FetchAsync(
            string providerType,
            string? apiKey,
            string? baseUrl = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var meta = ProviderRegistry.GetMetadata(providerType);
                return await meta.FetchLiveModelsAsync(apiKey, baseUrl, cancellationToken);
            }
            catch (OperationCanceledException) { return null; }
            catch { return null; }
        }
    }
}
