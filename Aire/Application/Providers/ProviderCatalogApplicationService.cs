using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Aire.AppLayer.Abstractions;
using Aire.Data;
using Aire.Services.Workflows;

namespace Aire.AppLayer.Providers
{
    /// <summary>
    /// Application-layer workflow for loading and selecting providers shown in the main provider picker.
    /// </summary>
    public sealed class ProviderCatalogApplicationService
    {
        /// <summary>
        /// Result of loading providers for the main picker.
        /// </summary>
        public sealed record ProviderCatalogResult(
            IReadOnlyList<Provider> AllProviders,
            IReadOnlyList<Provider> EnabledProviders,
            Provider? SelectedProvider,
            string? EmptyStateMessage);

        private readonly IProviderRepository _providers;
        private readonly ProviderSelectionWorkflowService _selectionWorkflow = new();

        /// <summary>
        /// Creates the provider-catalog application service over the provider repository boundary.
        /// </summary>
        public ProviderCatalogApplicationService(IProviderRepository providers)
        {
            _providers = providers;
        }

        /// <summary>
        /// Loads providers, filters them to enabled entries, and resolves which one should be selected.
        /// </summary>
        public async Task<ProviderCatalogResult> LoadProviderCatalogAsync(bool autoSelect, int? savedProviderId)
        {
            var allProviders = await _providers.GetProvidersAsync();
            var enabledProviders = _selectionWorkflow.GetEnabledProviders(allProviders);
            var selectedProvider = _selectionWorkflow.ResolveSelectedProvider(enabledProviders, savedProviderId, autoSelect);
            var emptyStateMessage = enabledProviders.Count == 0
                ? _selectionWorkflow.BuildNoProviderMessage()
                : null;

            return new ProviderCatalogResult(allProviders, enabledProviders, selectedProvider, emptyStateMessage);
        }

        /// <summary>
        /// Resolves the provider that should remain selected after the picker is refreshed.
        /// </summary>
        public Provider? ResolveSelectionAfterRefresh(IEnumerable<Provider> enabledProviders, int? selectedProviderId)
            => selectedProviderId.HasValue
                ? enabledProviders.FirstOrDefault(p => p.Id == selectedProviderId.Value)
                : null;
    }
}
