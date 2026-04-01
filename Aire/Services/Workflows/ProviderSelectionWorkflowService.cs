using System.Collections.Generic;
using System.Linq;
using Aire.Data;

namespace Aire.Services.Workflows
{
    /// <summary>
    /// Encapsulates provider-picker decisions so the UI only has to render the result.
    /// </summary>
    public sealed class ProviderSelectionWorkflowService
    {
        /// <summary>
        /// Filters the database list down to providers that are currently enabled for chat use.
        /// </summary>
        /// <param name="providers">All providers loaded from persistence.</param>
        /// <returns>Only the providers that are currently enabled.</returns>
        public IReadOnlyList<Provider> GetEnabledProviders(IEnumerable<Provider> providers) =>
            providers.Where(p => p.IsEnabled).ToList();

        /// <summary>
        /// Chooses the provider that should become active when the provider list is loaded.
        /// </summary>
        /// <param name="enabledProviders">Providers that are eligible for selection.</param>
        /// <param name="savedProviderId">Previously selected provider id, if one was persisted.</param>
        /// <param name="autoSelect">Whether the first available provider should be selected when there is no saved one.</param>
        /// <returns>The provider that should become active, or <see langword="null"/> when none should be selected.</returns>
        public Provider? ResolveSelectedProvider(
            IReadOnlyList<Provider> enabledProviders,
            int? savedProviderId,
            bool autoSelect)
        {
            if (savedProviderId.HasValue)
            {
                var saved = enabledProviders.FirstOrDefault(p => p.Id == savedProviderId.Value);
                if (saved != null)
                    return saved;
            }

            if (autoSelect && enabledProviders.Count > 0)
                return enabledProviders[0];

            return null;
        }

        /// <summary>
        /// User-facing message shown when no providers are available.
        /// </summary>
        public string BuildNoProviderMessage() =>
            "No supported AI providers found. Open Settings (\u2699) to configure a provider.";

        /// <summary>
        /// User-facing confirmation shown after the active provider changes.
        /// </summary>
        /// <param name="providerName">Display name of the provider that was activated.</param>
        /// <returns>A short confirmation message suitable for the chat transcript.</returns>
        public string BuildSwitchedProviderMessage(string providerName) =>
            $"Switched to {providerName}";
    }
}
