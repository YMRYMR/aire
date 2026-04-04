using System;
using System.Threading;
using System.Threading.Tasks;
using Aire.AppLayer.Abstractions;
using Aire.Domain.Providers;

namespace Aire.AppLayer.Providers
{
    /// <summary>
    /// Application-layer workflow for installing the local Codex CLI bridge.
    /// </summary>
    public sealed class CodexActionApplicationService
    {
        private readonly ICodexManagementClient _client;

        /// <summary>
        /// Creates the Codex install workflow over the infrastructure management port.
        /// </summary>
        public CodexActionApplicationService(ICodexManagementClient client)
        {
            _client = client;
        }

        /// <summary>
        /// Reports whether a launchable Codex CLI is already available.
        /// </summary>
        public Task<CodexCliStatus> GetStatusAsync(CancellationToken cancellationToken = default)
            => _client.GetStatusAsync(cancellationToken);

        /// <summary>
        /// Installs the Codex CLI and returns a normalized user-facing result.
        /// </summary>
        public async Task<CodexActionResult> InstallAsync(IProgress<string>? progress = null, CancellationToken cancellationToken = default)
        {
            try
            {
                await _client.InstallAsync(progress, cancellationToken);
                return new CodexActionResult(
                    true,
                    "Codex CLI installed. If this is your first time, run 'codex login' in a terminal, then restart Aire.");
            }
            catch
            {
            return new CodexActionResult(false, "Could not install Codex CLI.");
            }
        }
    }
}
