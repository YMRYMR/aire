using System;
using System.Threading;
using System.Threading.Tasks;
using Aire.AppLayer.Abstractions;

namespace Aire.Services.Providers
{
    /// <summary>
    /// Default infrastructure adapter for Ollama management actions used by the application layer.
    /// </summary>
    public sealed class OllamaManagementClient : IOllamaManagementClient
    {
        /// <inheritdoc />
        public async Task InstallAsync(IProgress<int>? progress = null, CancellationToken cancellationToken = default)
        {
            using var service = new OllamaService();
            await service.InstallOllamaAsync(progress, cancellationToken);
        }

        /// <inheritdoc />
        public async Task PullModelAsync(
            string modelName,
            string? baseUrl = null,
            IProgress<OllamaService.OllamaPullProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            using var service = new OllamaService();
            await service.PullModelAsync(modelName, baseUrl, progress, cancellationToken);
        }

        /// <inheritdoc />
        public async Task DeleteModelAsync(string modelName, string? baseUrl = null, CancellationToken cancellationToken = default)
        {
            using var service = new OllamaService();
            await service.DeleteModelAsync(modelName, baseUrl, cancellationToken);
        }
    }
}
