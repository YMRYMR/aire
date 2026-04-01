using Aire.Data;
using Aire.Providers;

namespace Aire.AppLayer.Providers
{
    /// <summary>
    /// Shared provider-model loading workflow used by onboarding and settings.
    /// </summary>
    public sealed class ProviderModelCatalogApplicationService
    {
        /// <summary>
        /// Result of loading provider models from built-in and optional live metadata.
        /// </summary>
        public sealed record ProviderModelCatalogResult(
            IReadOnlyList<ModelDefinition> DefaultModels,
            IReadOnlyList<ModelDefinition> EffectiveModels,
            bool UsedLiveModels,
            string? StatusMessage);

        /// <summary>
        /// Loads model definitions for one provider, preferring live models when they are available.
        /// </summary>
        public async Task<ProviderModelCatalogResult> LoadModelsAsync(
            IProviderMetadata metadata,
            string? apiKey,
            string? baseUrl,
            CancellationToken cancellationToken = default)
        {
            var defaultModels = metadata.GetDefaultModels();

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                return new ProviderModelCatalogResult(
                    defaultModels,
                    defaultModels,
                    UsedLiveModels: false,
                    StatusMessage: null);
            }

            try
            {
                var liveModels = await metadata.FetchLiveModelsAsync(apiKey, baseUrl, cancellationToken);
                if (liveModels != null && liveModels.Count > 0)
                {
                    return new ProviderModelCatalogResult(
                        defaultModels,
                        liveModels,
                        UsedLiveModels: true,
                        StatusMessage: $"✓ {liveModels.Count} models fetched from {metadata.ProviderType}");
                }

                return new ProviderModelCatalogResult(
                    defaultModels,
                    defaultModels,
                    UsedLiveModels: false,
                    StatusMessage: "Could not fetch models — showing built-in list");
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                return new ProviderModelCatalogResult(
                    defaultModels,
                    defaultModels,
                    UsedLiveModels: false,
                    StatusMessage: "Could not fetch models — showing built-in list");
            }
        }
    }
}
