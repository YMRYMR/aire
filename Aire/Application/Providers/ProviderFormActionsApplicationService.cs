using System;
using System.Threading;
using System.Threading.Tasks;
using Aire.Data;
using Aire.Domain.Providers;
using Aire.Providers;
using Aire.Services.Providers;

namespace Aire.AppLayer.Providers
{
    /// <summary>
    /// Shared application-layer workflow for provider form actions in settings/onboarding.
    /// Keeps model refresh/import and provider-tool install rules out of WPF coordinators.
    /// </summary>
    public sealed class ProviderFormActionsApplicationService
    {
        /// <summary>
        /// Describes the current install state for one provider-owned local tool.
        /// </summary>
        public sealed record ProviderToolStatus(
            bool IsInstalled,
            string ActionLabel,
            string StatusMessage);

        private readonly ProviderModelCatalogApplicationService _modelCatalogService = new();
        private readonly CodexActionApplicationService _codexActionService;

        /// <summary>
        /// Creates the provider form workflow over the default Codex management client.
        /// </summary>
        public ProviderFormActionsApplicationService()
            : this(new CodexActionApplicationService(new CodexManagementClient()))
        {
        }

        /// <summary>
        /// Creates the provider form workflow over an injected Codex action workflow.
        /// </summary>
        /// <param name="codexActionService">Shared Codex install workflow.</param>
        public ProviderFormActionsApplicationService(CodexActionApplicationService codexActionService)
        {
            _codexActionService = codexActionService;
        }

        /// <summary>
        /// Loads the effective model catalog for one provider form.
        /// </summary>
        /// <param name="metadata">Provider metadata used to fetch default/live models.</param>
        /// <param name="apiKey">Current API key entered in the form.</param>
        /// <param name="baseUrl">Current base URL entered in the form.</param>
        /// <param name="cancellationToken">Cancellation token for any live-model fetch.</param>
        /// <returns>The shared model catalog result for the UI to render.</returns>
        public Task<ProviderModelCatalogApplicationService.ProviderModelCatalogResult> LoadModelsAsync(
            IProviderMetadata metadata,
            string? apiKey,
            string? baseUrl,
            CancellationToken cancellationToken = default)
            => _modelCatalogService.LoadModelsAsync(metadata, apiKey, baseUrl, cancellationToken);

        /// <summary>
        /// Imports model definitions from a JSON file into the shared model catalog.
        /// </summary>
        /// <param name="filePath">Path to the imported JSON file.</param>
        public void ImportModels(string filePath)
            => ModelCatalog.ImportFile(filePath);

        /// <summary>
        /// Returns the install state for any provider-owned helper tool surfaced by the settings form.
        /// </summary>
        /// <param name="providerType">Canonical provider type currently being edited.</param>
        public ProviderToolStatus? GetProviderToolStatus(string providerType)
        {
            if (!string.Equals(providerType, "Codex", StringComparison.OrdinalIgnoreCase))
                return null;

            var status = CodexProvider.GetCliStatus();
            return new ProviderToolStatus(
                status.IsInstalled,
                "Install Codex CLI",
                status.IsInstalled
                    ? "Codex CLI detected. You can test the connection now."
                    : status.UserMessage);
        }

        /// <summary>
        /// Installs any provider-owned helper tool exposed by the settings form.
        /// </summary>
        /// <param name="providerType">Canonical provider type currently being edited.</param>
        /// <param name="progress">Optional progress callback for user-visible status text.</param>
        /// <param name="cancellationToken">Cancellation token for the install action.</param>
        /// <returns>A normalized provider action result.</returns>
        public async Task<CodexActionResult> InstallProviderToolAsync(
            string providerType,
            IProgress<string>? progress = null,
            CancellationToken cancellationToken = default)
        {
            if (!string.Equals(providerType, "Codex", StringComparison.OrdinalIgnoreCase))
                return new CodexActionResult(false, $"No installable provider tool is available for '{providerType}'.");

            return await _codexActionService.InstallAsync(progress, cancellationToken).ConfigureAwait(false);
        }
    }
}
