using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Aire.AppLayer.Abstractions;
using Aire.Data;

namespace Aire.AppLayer.Providers
{
    /// <summary>
    /// Owns the provider-list workflow used by the settings window.
    /// This keeps add/delete/reorder/reselection rules out of the WPF event handlers.
    /// </summary>
    public sealed class SettingsProviderListApplicationService
    {
        /// <summary>
        /// Result of loading the settings provider list and resolving which provider should remain selected.
        /// </summary>
        /// <param name="Providers">Providers as they should be shown in the list.</param>
        /// <param name="SelectedProvider">Provider that should remain selected after the refresh, if any.</param>
        public sealed record ProviderListState(
            IReadOnlyList<Provider> Providers,
            Provider? SelectedProvider);

        /// <summary>
        /// Result of applying a drag-and-drop reorder operation to the provider list.
        /// </summary>
        /// <param name="Providers">Providers in their reordered display order.</param>
        /// <param name="SelectedProvider">Provider that should remain selected after reordering.</param>
        /// <param name="OrderChanged">Whether the operation produced a real order change that should be persisted.</param>
        public sealed record ReorderResult(
            IReadOnlyList<Provider> Providers,
            Provider? SelectedProvider,
            bool OrderChanged);

        /// <summary>
        /// Loads providers for the settings list and resolves which provider should remain selected.
        /// </summary>
        /// <param name="providers">Repository used to load stored providers.</param>
        /// <param name="reselectId">Provider to prefer after a refresh, such as a newly inserted provider.</param>
        /// <param name="currentSelectedId">Currently selected provider, used when there is no explicit reselection target.</param>
        /// <returns>Provider list plus the provider that should remain selected.</returns>
        public async Task<ProviderListState> LoadAsync(
            IProviderRepository providers,
            int? reselectId = null,
            int? currentSelectedId = null)
        {
            var allProviders = await providers.GetProvidersAsync();
            var selectedProvider = ResolveSelectedProvider(allProviders, reselectId, currentSelectedId);
            return new ProviderListState(allProviders, selectedProvider);
        }

        /// <summary>
        /// Creates and persists a default provider entry for the settings editor.
        /// </summary>
        /// <param name="providers">Repository used to insert the new provider.</param>
        /// <returns>The inserted provider, including its generated identifier.</returns>
        public async Task<Provider> CreateDefaultProviderAsync(IProviderRepository providers)
        {
            var provider = new Provider
            {
                Name = "New Provider",
                Type = "OpenAI",
                Model = "gpt-4o",
                IsEnabled = true,
                Color = "#888888"
            };
            provider.Id = await providers.InsertProviderAsync(provider);
            return provider;
        }

        /// <summary>
        /// Deletes a provider from the repository.
        /// </summary>
        /// <param name="providers">Repository used to delete the provider.</param>
        /// <param name="providerId">Identifier of the provider to remove.</param>
        public Task DeleteProviderAsync(IProviderRepository providers, int providerId)
            => providers.DeleteProviderAsync(providerId);

        /// <summary>
        /// Reorders providers according to a drag-and-drop move and returns the updated selection state.
        /// </summary>
        /// <param name="providers">Current providers shown in the list.</param>
        /// <param name="draggedProviderId">Provider being dragged.</param>
        /// <param name="targetProviderId">Provider currently under the drop target.</param>
        /// <returns>The reordered list state and whether the new order should be persisted.</returns>
        public ReorderResult Reorder(
            IReadOnlyList<Provider> providers,
            int draggedProviderId,
            int targetProviderId)
        {
            var reordered = providers.ToList();
            var dragged = reordered.FirstOrDefault(p => p.Id == draggedProviderId);
            var target = reordered.FirstOrDefault(p => p.Id == targetProviderId);
            if (dragged == null || target == null || dragged.Id == target.Id)
            {
                return new ReorderResult(reordered, dragged, false);
            }

            var oldIndex = reordered.IndexOf(dragged);
            var newIndex = reordered.IndexOf(target);
            if (oldIndex < 0 || newIndex < 0 || oldIndex == newIndex)
            {
                return new ReorderResult(reordered, dragged, false);
            }

            reordered.RemoveAt(oldIndex);
            reordered.Insert(newIndex, dragged);
            return new ReorderResult(reordered, dragged, true);
        }

        /// <summary>
        /// Persists a reordered provider list.
        /// </summary>
        /// <param name="providers">Repository used to save the new provider order.</param>
        /// <param name="orderedProviders">Providers in their updated order.</param>
        public Task SaveOrderAsync(IProviderRepository providers, IEnumerable<Provider> orderedProviders)
            => providers.SaveProviderOrderAsync(orderedProviders);

        private static Provider? ResolveSelectedProvider(
            IReadOnlyList<Provider> providers,
            int? reselectId,
            int? currentSelectedId)
        {
            if (reselectId.HasValue)
            {
                return providers.FirstOrDefault(p => p.Id == reselectId.Value);
            }

            if (currentSelectedId.HasValue)
            {
                return providers.FirstOrDefault(p => p.Id == currentSelectedId.Value);
            }

            return null;
        }
    }
}
