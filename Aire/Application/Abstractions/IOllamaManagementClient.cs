using System;
using System.Threading;
using System.Threading.Tasks;
using Aire.Services;

namespace Aire.AppLayer.Abstractions
{
    /// <summary>
    /// Application-layer port for Ollama management actions that mutate the local runtime or installed model set.
    /// </summary>
    public interface IOllamaManagementClient
    {
        /// <summary>
        /// Downloads and installs Ollama on the current machine.
        /// </summary>
        Task InstallAsync(IProgress<int>? progress = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Pulls one Ollama model into the local runtime.
        /// </summary>
        Task PullModelAsync(string modelName, string? baseUrl = null, IProgress<OllamaService.OllamaPullProgress>? progress = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes one installed Ollama model from the local runtime.
        /// </summary>
        Task DeleteModelAsync(string modelName, string? baseUrl = null, CancellationToken cancellationToken = default);
    }
}
