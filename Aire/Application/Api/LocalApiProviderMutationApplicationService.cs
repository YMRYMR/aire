using System;
using System.Linq;
using System.Threading.Tasks;
using Aire.AppLayer.Abstractions;
using Aire.Data;
using Aire.AppLayer.Providers;

namespace Aire.AppLayer.Api
{
    /// <summary>
    /// Shared application-layer workflow for local-API provider mutations.
    /// Keeps provider create/update mutation rules and resulting refresh decisions out of MainWindow API handlers.
    /// </summary>
    public sealed class LocalApiProviderMutationApplicationService
    {
        /// <summary>
        /// Result of creating one provider through the local API.
        /// </summary>
        public sealed record ProviderCreationFlowResult(
            Provider Provider,
            bool IsDuplicate,
            bool RefreshProviderCatalog,
            bool RefreshSettingsProviderList,
            int? ReselectProviderId,
            int? SelectProviderId);

        /// <summary>
        /// Result of updating one provider model through the local API.
        /// </summary>
        public sealed record ProviderModelUpdateFlowResult(
            bool Updated,
            Provider? Provider,
            bool RefreshProviderCatalog,
            bool RefreshSettingsProviderList,
            bool RefreshActiveProvider);

        private readonly ProviderCreationApplicationService _providerCreationService = new();

        /// <summary>
        /// Creates a provider and returns the UI-refresh effects that should follow.
        /// </summary>
        public async Task<ProviderCreationFlowResult> CreateProviderAsync(
            IProviderRepository providerRepository,
            ProviderCreationApplicationService.ProviderCreationRequest request,
            bool selectAfterCreate)
        {
            var creation = await _providerCreationService.CreateAsync(providerRepository, request).ConfigureAwait(false);
            return new ProviderCreationFlowResult(
                creation.Provider,
                creation.IsDuplicate,
                RefreshProviderCatalog: !creation.IsDuplicate,
                RefreshSettingsProviderList: !creation.IsDuplicate,
                ReselectProviderId: creation.IsDuplicate ? null : creation.Provider.Id,
                SelectProviderId: selectAfterCreate && !creation.IsDuplicate ? creation.Provider.Id : null);
        }

        /// <summary>
        /// Updates one provider model and returns the UI-refresh effects that should follow.
        /// </summary>
        public async Task<ProviderModelUpdateFlowResult> UpdateProviderModelAsync(
            IProviderRepository providerRepository,
            int providerId,
            string normalizedModel,
            int? activeProviderId)
        {
            var providers = await providerRepository.GetProvidersAsync().ConfigureAwait(false);
            var provider = providers.FirstOrDefault(p => p.Id == providerId);
            if (provider == null)
                return new ProviderModelUpdateFlowResult(false, null, false, false, false);

            provider.Model = normalizedModel;
            await providerRepository.UpdateProviderAsync(provider).ConfigureAwait(false);

            var isActiveProvider = providerId == activeProviderId;
            return new ProviderModelUpdateFlowResult(
                true,
                provider,
                RefreshProviderCatalog: !isActiveProvider,
                RefreshSettingsProviderList: true,
                RefreshActiveProvider: isActiveProvider);
        }
    }
}
