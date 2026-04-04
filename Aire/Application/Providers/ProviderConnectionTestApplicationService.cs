using System.Threading;
using System.Threading.Tasks;
using Aire.Providers;

namespace Aire.AppLayer.Providers
{
    /// <summary>
    /// Shared application-layer workflow for lightweight provider connection tests.
    /// Keeps validation and smoke-test policy out of onboarding/settings event handlers.
    /// </summary>
    public sealed class ProviderConnectionTestApplicationService
    {
        /// <summary>
        /// Result of one provider connection test.
        /// </summary>
        /// <param name="Success">Whether the provider accepted the configuration.</param>
        /// <param name="Message">User-facing summary of the outcome.</param>
        public sealed record ConnectionTestResult(bool Success, string Message);

        private readonly ProviderSetupApplicationService _providerSetupService;

        /// <summary>
        /// Creates the shared provider connection-test workflow over the default provider setup service.
        /// </summary>
        public ProviderConnectionTestApplicationService()
            : this(new ProviderSetupApplicationService())
        {
        }

        /// <summary>
        /// Creates the shared provider connection-test workflow over an injected provider setup service.
        /// </summary>
        /// <param name="providerSetupService">Shared provider setup workflow used for validation and smoke tests.</param>
        public ProviderConnectionTestApplicationService(ProviderSetupApplicationService providerSetupService)
        {
            _providerSetupService = providerSetupService;
        }

        /// <summary>
        /// Validates one configured provider and runs a minimal smoke test when valid.
        /// </summary>
        /// <param name="provider">Configured provider instance to probe.</param>
        /// <param name="cancellationToken">Cancellation token for the validation/probe request.</param>
        /// <returns>A user-facing connection result suitable for onboarding/settings feedback.</returns>
        public async Task<ConnectionTestResult> RunAsync(IAiProvider provider, CancellationToken cancellationToken)
        {
            var validation = await _providerSetupService.ValidateDetailedAsync(provider, cancellationToken).ConfigureAwait(false);
            if (!validation.IsValid)
            {
                var message = validation.ErrorMessage ?? "Provider configuration is invalid.";
                if (!string.IsNullOrWhiteSpace(validation.RemediationHint))
                    message = $"{message} {validation.RemediationHint}";

                return new ConnectionTestResult(false, message);
            }

            var smokeTest = await _providerSetupService.RunSmokeTestAsync(provider, cancellationToken).ConfigureAwait(false);
            return smokeTest.Success
                ? new ConnectionTestResult(true, "Connected!")
                : new ConnectionTestResult(false, smokeTest.ErrorMessage ?? "Connection test failed.");
        }
    }
}
