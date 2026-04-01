using System.Threading;
using System.Threading.Tasks;
using Aire.Domain.Providers;

namespace Aire.AppLayer.Abstractions
{
    /// <summary>
    /// Infrastructure port for installing and checking the local Codex CLI.
    /// </summary>
    public interface ICodexManagementClient
    {
        /// <summary>
        /// Returns whether a launchable Codex CLI is already available.
        /// </summary>
        Task<CodexCliStatus> GetStatusAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Installs the Codex CLI on the current machine.
        /// </summary>
        Task InstallAsync(IProgress<string>? progress = null, CancellationToken cancellationToken = default);
    }
}
