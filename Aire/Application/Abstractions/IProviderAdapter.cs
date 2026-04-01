using System.Threading;
using System.Threading.Tasks;
using Aire.Domain.Providers;
using Aire.Providers;

namespace Aire.AppLayer.Abstractions
{
    /// <summary>
    /// Application-facing adapter contract for one provider family.
    /// Aire owns the workflow semantics; implementations own the provider-specific
    /// request packing, validation, and runtime quirks needed to satisfy them.
    /// </summary>
    public interface IProviderAdapter
    {
        /// <summary>
        /// Stable provider type handled by this adapter, such as OpenAI, Ollama, or Codex.
        /// </summary>
        string ProviderType { get; }

        /// <summary>
        /// Returns whether this adapter should handle the supplied provider type.
        /// </summary>
        /// <param name="providerType">Persisted provider type identifier.</param>
        bool CanHandle(string providerType);

        /// <summary>
        /// Builds a configured runtime provider from normalized form state.
        /// Current implementations may still bridge to the legacy provider runtime path
        /// until the full shared provider-semantic contracts are migrated.
        /// </summary>
        /// <param name="request">Normalized provider runtime request.</param>
        IAiProvider? BuildProvider(ProviderRuntimeRequest request);

        /// <summary>
        /// Runs a lightweight connectivity or smoke-test probe for the adapter.
        /// </summary>
        /// <param name="provider">Configured runtime provider to probe.</param>
        /// <param name="cancellationToken">Cancellation token for the probe.</param>
        Task<ProviderSmokeTestResult> RunSmokeTestAsync(IAiProvider provider, CancellationToken cancellationToken);
    }
}
