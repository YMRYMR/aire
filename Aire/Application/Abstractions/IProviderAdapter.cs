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
        /// Executes one provider turn using Aire's shared request semantics and returns
        /// a provider-independent execution result.
        /// </summary>
        /// <param name="provider">Configured runtime provider to execute.</param>
        /// <param name="requestContext">Shared request context for the turn.</param>
        Task<ProviderExecutionResult> ExecuteAsync(IAiProvider provider, ProviderRequestContext requestContext);

        /// <summary>
        /// Runs a lightweight connectivity or smoke-test probe for the adapter.
        /// </summary>
        /// <param name="provider">Configured runtime provider to probe.</param>
        /// <param name="cancellationToken">Cancellation token for the probe.</param>
        Task<ProviderSmokeTestResult> RunSmokeTestAsync(IAiProvider provider, CancellationToken cancellationToken);

        /// <summary>
        /// Validates a configured runtime provider and classifies any failure into
        /// Aire's shared validation semantics.
        /// </summary>
        /// <param name="provider">Configured runtime provider to validate.</param>
        /// <param name="cancellationToken">Cancellation token for the validation request.</param>
        Task<ProviderValidationOutcome> ValidateAsync(IAiProvider provider, CancellationToken cancellationToken);
    }
}
