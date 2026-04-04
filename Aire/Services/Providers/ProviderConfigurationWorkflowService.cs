using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Aire.AppLayer.Abstractions;
using Aire.Data;
using Aire.Domain.Providers;
using Aire.Providers;

namespace Aire.Services.Providers
{
    /// <summary>
    /// Centralizes provider form rules that are shared by onboarding and settings.
    /// This keeps provider creation, save validation, and model normalization out of WPF event handlers.
    /// </summary>
    public sealed class ProviderConfigurationWorkflowService
    {
        /// <summary>
        /// Builds a configured runtime provider from user-entered fields.
        /// </summary>
        /// <param name="request">
        /// Normalized provider form state, including provider type, credentials, base URL, selected model,
        /// and whether a Claude web session is already available.
        /// </param>
        /// <returns>
        /// A ready-to-use provider instance, or <see langword="null"/> when the form does not yet contain
        /// enough information to create one.
        /// </returns>
        public IAiProvider? CreateRuntimeProvider(ProviderRuntimeRequest request)
        {
            var type = string.IsNullOrWhiteSpace(request.Type) ? "OpenAI" : request.Type;
            var apiKey = request.ApiKey?.Trim();
            var baseUrl = string.IsNullOrWhiteSpace(request.BaseUrl) ? null : request.BaseUrl.Trim();
            var descriptor = ProviderCatalog.TryGetDescriptor(type, out var resolved)
                ? resolved
                : ProviderCatalog.GetDescriptor("OpenAI");

            if (descriptor.RequiresApiKey && string.IsNullOrWhiteSpace(apiKey))
                return null;

            if (descriptor.SupportsSessionCredential && descriptor.Type == "ClaudeWeb" && !request.ClaudeWebSessionReady)
                return null;

            if (descriptor.SupportsSessionCredential && string.IsNullOrWhiteSpace(apiKey) && request.ClaudeWebSessionReady && descriptor.Type == "ClaudeWeb")
                apiKey = "claude.ai-session";

            IAiProvider provider = descriptor.CreateRuntimeProvider();

            provider.Initialize(new ProviderConfig
            {
                ApiKey = apiKey ?? string.Empty,
                BaseUrl = baseUrl,
                Model = request.Model
            });

            return provider;
        }

        /// <summary>
        /// Inserts a new provider after applying the same duplicate rules used by onboarding.
        /// </summary>
        /// <param name="providerRepository">Provider repository used to read existing providers and insert the new one.</param>
        /// <param name="draft">Persistable provider values collected from the UI.</param>
        /// <returns>
        /// A result describing whether a provider was saved and whether the save was blocked by an existing duplicate.
        /// </returns>
        public async Task<ProviderPersistResult> SaveNewProviderAsync(IProviderRepository providerRepository, ProviderDraft draft)
        {
            var existing = await providerRepository.GetProvidersAsync();
            var isDuplicate = existing.Any(p =>
                string.Equals(p.Type, draft.Type, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(p.Model, draft.Model, StringComparison.OrdinalIgnoreCase));

            if (isDuplicate)
                return new ProviderPersistResult(false, true);

            var provider = new Provider
            {
                Name = draft.Name,
                Type = draft.Type,
                ApiKey = draft.ApiKey,
                BaseUrl = draft.BaseUrl,
                Model = draft.Model,
                IsEnabled = draft.IsEnabled,
                Color = draft.Color,
            };

            provider.Id = await providerRepository.InsertProviderAsync(provider);
            return new ProviderPersistResult(true, false, provider.Id);
        }

        /// <summary>
        /// Applies edited form values back onto an existing provider record.
        /// </summary>
        /// <param name="provider">The provider entity being edited.</param>
        /// <param name="name">User-visible provider name from the form.</param>
        /// <param name="type">Selected provider type identifier.</param>
        /// <param name="apiKey">Credential text currently entered in the form.</param>
        /// <param name="baseUrl">Optional provider base URL from the form.</param>
        /// <param name="rawModelText">Visible model text currently shown to the user.</param>
        /// <param name="selectedModelValue">Canonical selected model id when the combo box provides one.</param>
        /// <param name="timeoutMinutes">Current request timeout value.</param>
        /// <param name="isEnabled">Whether the provider should remain enabled for chat use.</param>
        /// <param name="knownModelMappings">
        /// Optional display-name to model-id mappings, used to strip UI decorations such as Ollama badges and sizes.
        /// </param>
        public void ApplyProviderEditorValues(
            Provider provider,
            string name,
            string type,
            string? apiKey,
            string? baseUrl,
            string rawModelText,
            string? selectedModelValue,
            int timeoutMinutes,
            bool isEnabled,
            IEnumerable<(string DisplayName, string ModelName)>? knownModelMappings = null)
        {
            provider.Name = name.Trim();
            provider.Type = string.IsNullOrWhiteSpace(type) ? "OpenAI" : type;
            provider.ApiKey = apiKey;
            provider.BaseUrl = string.IsNullOrWhiteSpace(baseUrl) ? string.Empty : baseUrl.Trim();
            provider.TimeoutMinutes = timeoutMinutes;
            provider.Model = NormalizeModelSelection(rawModelText, selectedModelValue, knownModelMappings);
            provider.IsEnabled = isEnabled;
        }

        /// <summary>
        /// Converts the visible model text into the canonical model id that should be stored.
        /// For Ollama this strips UI decorations such as checkmarks and size labels.
        /// </summary>
        /// <param name="rawModelText">Text currently shown in the model input.</param>
        /// <param name="selectedModelValue">Canonical selected value from the combo box, when one exists.</param>
        /// <param name="knownModelMappings">
        /// Optional display-name to model-id mappings for decorated model lists.
        /// </param>
        /// <returns>The model id that should be persisted on the provider record.</returns>
        public string NormalizeModelSelection(
            string rawModelText,
            string? selectedModelValue,
            IEnumerable<(string DisplayName, string ModelName)>? knownModelMappings = null)
        {
            if (!string.IsNullOrWhiteSpace(selectedModelValue))
                return selectedModelValue;

            var modelValue = rawModelText.Trim();
            if (knownModelMappings == null)
                return modelValue;

            var cleanText = modelValue.StartsWith("✓ ", StringComparison.Ordinal) ? modelValue[2..].Trim() : modelValue;
            var sizeSuffixIndex = cleanText.LastIndexOf("  (", StringComparison.Ordinal);
            if (sizeSuffixIndex > 0)
                cleanText = cleanText[..sizeSuffixIndex].Trim();

            var match = knownModelMappings.FirstOrDefault(item =>
                string.Equals(item.DisplayName, modelValue, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(item.DisplayName, cleanText, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(item.ModelName, modelValue, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(item.ModelName, cleanText, StringComparison.OrdinalIgnoreCase));

            return string.IsNullOrWhiteSpace(match.ModelName) ? modelValue : match.ModelName;
        }
    }
}
