using System;
using System.Threading;
using System.Threading.Tasks;
using Aire.AppLayer.Abstractions;
using Aire.Domain.Providers;
using Aire.Services;

namespace Aire.AppLayer.Providers
{
    /// <summary>
    /// Application-layer workflow for Ollama install, download, and uninstall actions shared by onboarding and settings.
    /// </summary>
    public sealed class OllamaActionApplicationService
    {
        private readonly IOllamaManagementClient _client;

        /// <summary>
        /// Creates the shared Ollama action workflow over the infrastructure management port.
        /// </summary>
        public OllamaActionApplicationService(IOllamaManagementClient client)
        {
            _client = client;
        }

        /// <summary>
        /// Installs Ollama on the current machine.
        /// </summary>
        /// <param name="progress">Optional progress reporter for UI-specific install updates.</param>
        /// <param name="cancellationToken">Cancellation token for the install workflow.</param>
        /// <returns>Normalized install outcome for the caller.</returns>
        public async Task<OllamaActionResult> InstallAsync(IProgress<int>? progress = null, CancellationToken cancellationToken = default)
        {
            try
            {
                await _client.InstallAsync(progress, cancellationToken);
                return new OllamaActionResult(true, "Ollama installed. Please restart the application.");
            }
            catch
            {
            return new OllamaActionResult(false, "Could not install Ollama.");
            }
        }

        /// <summary>
        /// Downloads one Ollama model into the selected local runtime.
        /// </summary>
        /// <param name="modelName">Canonical Ollama model name to download.</param>
        /// <param name="baseUrl">Optional Ollama base URL for the target runtime.</param>
        /// <param name="progress">Optional progress reporter for UI-specific download updates.</param>
        /// <param name="cancellationToken">Cancellation token for the download workflow.</param>
        /// <returns>Normalized download outcome for the caller.</returns>
        public async Task<OllamaActionResult> DownloadModelAsync(
            string modelName,
            string? baseUrl = null,
            IProgress<OllamaService.OllamaPullProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                await _client.PullModelAsync(modelName, baseUrl, progress, cancellationToken);
                return new OllamaActionResult(true, $"'{modelName}' downloaded successfully.");
            }
            catch
            {
            return new OllamaActionResult(false, "Could not download model.");
            }
        }

        /// <summary>
        /// Uninstalls one Ollama model from the selected local runtime.
        /// </summary>
        /// <param name="modelName">Canonical Ollama model name to delete.</param>
        /// <param name="baseUrl">Optional Ollama base URL for the target runtime.</param>
        /// <param name="cancellationToken">Cancellation token for the uninstall workflow.</param>
        /// <returns>Normalized uninstall outcome for the caller.</returns>
        public async Task<OllamaActionResult> UninstallModelAsync(string modelName, string? baseUrl = null, CancellationToken cancellationToken = default)
        {
            try
            {
                await _client.DeleteModelAsync(modelName, baseUrl, cancellationToken);
                return new OllamaActionResult(true, $"'{modelName}' uninstalled successfully.");
            }
            catch
            {
            return new OllamaActionResult(false, "Could not uninstall model.");
            }
        }
    }
}
