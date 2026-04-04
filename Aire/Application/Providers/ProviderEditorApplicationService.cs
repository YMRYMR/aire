using System;
using System.Collections.Generic;
using System.Linq;
using Aire.Data;
using Aire.Providers;

namespace Aire.AppLayer.Providers
{
    /// <summary>
    /// Application-layer mapping between stored provider records and the settings/onboarding editor state.
    /// </summary>
    public sealed class ProviderEditorApplicationService
    {
        /// <summary>
        /// Describes which model-loading path the UI should take after a provider is selected.
        /// </summary>
        public enum ModelLoadAction
        {
            None,
            LoadMetadataModels,
            LoadOllamaModels,
            SyncExistingOllamaItems
        }

        /// <summary>
        /// View-neutral editor state derived from a selected provider.
        /// </summary>
        public sealed record ProviderEditorSelectionPlan(
            string Name,
            string Type,
            string ApiKey,
            string BaseUrl,
            string Model,
            bool IsEnabled,
            bool HasApiKey,
            IProviderMetadata Metadata,
            ModelLoadAction ModelAction);

        /// <summary>
        /// View-neutral state for keeping an Ollama selection in sync with the current model list.
        /// </summary>
        public sealed record OllamaSelectionPlan(
            string? SelectedModelName,
            bool EnableDownloadButton);

        /// <summary>
        /// View-neutral state for handling a provider-type change in the settings editor.
        /// </summary>
        public sealed record ProviderTypeChangePlan(
            IProviderMetadata Metadata,
            ModelLoadAction ModelAction);

        /// <summary>
        /// Builds the editor state for a selected provider and indicates which model-loading path the UI should take.
        /// </summary>
        /// <param name="provider">Selected provider record.</param>
        /// <param name="isRefreshing">Whether the provider list is currently being rebuilt and should avoid expensive reloads.</param>
        /// <returns>View-neutral editor state and the model-loading action the UI should execute.</returns>
        public ProviderEditorSelectionPlan BuildSelectionPlan(Provider provider, bool isRefreshing)
        {
            var metadata = ProviderFactory.GetMetadata(provider.Type);
            var action = provider.Type switch
            {
                "Ollama" when !isRefreshing => ModelLoadAction.LoadOllamaModels,
                "Ollama" => ModelLoadAction.SyncExistingOllamaItems,
                _ when !isRefreshing => ModelLoadAction.LoadMetadataModels,
                _ => ModelLoadAction.None
            };

            return new ProviderEditorSelectionPlan(
                provider.Name,
                provider.Type,
                provider.ApiKey ?? string.Empty,
                provider.BaseUrl ?? string.Empty,
                provider.Model,
                provider.IsEnabled,
                !string.IsNullOrEmpty(provider.ApiKey),
                metadata,
                action);
        }

        /// <summary>
        /// Resolves how the settings UI should treat the current Ollama model against an existing model list.
        /// </summary>
        /// <param name="availableModelNames">Canonical model names currently shown in the dropdown.</param>
        /// <param name="selectedModel">Stored provider model that should be reflected in the UI.</param>
        /// <returns>The matching model name, plus whether the download button should remain enabled.</returns>
        public OllamaSelectionPlan BuildOllamaSelectionPlan(IEnumerable<string> availableModelNames, string? selectedModel)
        {
            var model = selectedModel ?? string.Empty;
            if (string.IsNullOrWhiteSpace(model))
                return new OllamaSelectionPlan(null, false);

            var match = availableModelNames.FirstOrDefault(name =>
                string.Equals(name, model, StringComparison.OrdinalIgnoreCase));

            return new OllamaSelectionPlan(
                match,
                match == null);
        }

        /// <summary>
        /// Resolves metadata and model-loading behavior after the selected provider type changes in the editor.
        /// </summary>
        /// <param name="providerType">Canonical provider type selected in the editor.</param>
        /// <param name="hasSelectedProvider">Whether the editor is currently bound to a persisted provider record.</param>
        /// <param name="isRefreshing">Whether the settings provider list is in a refresh cycle and should avoid extra model reloads.</param>
        public ProviderTypeChangePlan BuildTypeChangePlan(string providerType, bool hasSelectedProvider, bool isRefreshing)
        {
            var metadata = ProviderFactory.GetMetadata(providerType);
            var action = providerType switch
            {
                "Ollama" => ModelLoadAction.None,
                _ when hasSelectedProvider && !isRefreshing => ModelLoadAction.LoadMetadataModels,
                _ => ModelLoadAction.None
            };

            return new ProviderTypeChangePlan(metadata, action);
        }
    }
}
