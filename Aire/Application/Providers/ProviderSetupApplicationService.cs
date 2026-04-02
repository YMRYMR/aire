using System.Threading;
using System.Threading.Tasks;
using Aire.AppLayer.Abstractions;
using Aire.Domain.Providers;
using Aire.Providers;
using Aire.Services.Providers;

namespace Aire.AppLayer.Providers
{
    /// <summary>
    /// Shared application-layer workflow for onboarding/settings provider setup actions.
    /// </summary>
    public sealed class ProviderSetupApplicationService
    {
        private readonly ProviderConfigurationWorkflowService _configurationWorkflow = new();
        private readonly ProviderRuntimeApplicationService _runtimeWorkflow;

        /// <summary>
        /// Creates the shared provider setup workflow over the default runtime gateway.
        /// </summary>
        public ProviderSetupApplicationService()
            : this(new ProviderRuntimeApplicationService())
        {
        }

        /// <summary>
        /// Creates the shared provider setup workflow over an injected runtime application service.
        /// </summary>
        /// <param name="runtimeWorkflow">Runtime application workflow used for provider creation and smoke tests.</param>
        public ProviderSetupApplicationService(ProviderRuntimeApplicationService runtimeWorkflow)
        {
            _runtimeWorkflow = runtimeWorkflow;
        }

        /// <summary>
        /// Builds a configured runtime provider from normalized form state.
        /// </summary>
        /// <param name="request">Normalized provider form values.</param>
        /// <returns>A provider ready for testing or capability probing, or <see langword="null"/> when the form is incomplete.</returns>
        public IAiProvider? BuildRuntimeProvider(ProviderRuntimeRequest request)
            => _runtimeWorkflow.BuildProvider(request);

        /// <summary>
        /// Runs a minimal connectivity check against a configured provider.
        /// </summary>
        /// <param name="provider">Configured provider instance.</param>
        /// <param name="cancellationToken">Cancellation token for the probe request.</param>
        /// <returns>Whether the provider accepted the configuration, plus optional failure text.</returns>
        public Task<ProviderSmokeTestResult> RunSmokeTestAsync(
            IAiProvider provider,
            CancellationToken cancellationToken)
            => _runtimeWorkflow.RunSmokeTestAsync(provider, cancellationToken);

        /// <summary>
        /// Validates a configured provider's API key and connectivity, returning a human-readable error when invalid.
        /// </summary>
        /// <param name="provider">Configured provider instance.</param>
        /// <param name="cancellationToken">Cancellation token for the validation request.</param>
        /// <returns>The validation result including an <see cref="ProviderValidationResult.Error"/> string when the configuration is invalid.</returns>
        public Task<ProviderValidationResult> ValidateAsync(
            IAiProvider provider,
            CancellationToken cancellationToken)
            => provider.ValidateConfigurationAsync(cancellationToken);

        /// <summary>
        /// Validates a configured provider using the adapter seam so failures can be
        /// classified into Aire's shared validation semantics.
        /// </summary>
        /// <param name="provider">Configured provider instance.</param>
        /// <param name="cancellationToken">Cancellation token for the validation request.</param>
        /// <returns>Enriched validation outcome including failure classification and remediation guidance.</returns>
        public Task<ProviderValidationOutcome> ValidateDetailedAsync(
            IAiProvider provider,
            CancellationToken cancellationToken)
            => _runtimeWorkflow.ValidateAsync(provider, cancellationToken);

        /// <summary>
        /// Saves a newly configured provider after applying duplicate checks shared with onboarding.
        /// </summary>
        /// <param name="providerRepository">Provider repository used for duplicate detection and insert.</param>
        /// <param name="draft">Persistable provider values collected from the UI.</param>
        /// <returns>Whether the provider was saved and whether it matched an existing duplicate.</returns>
        public Task<ProviderPersistResult> SaveNewProviderAsync(
            IProviderRepository providerRepository,
            ProviderDraft draft)
            => _configurationWorkflow.SaveNewProviderAsync(providerRepository, draft);
    }
}
