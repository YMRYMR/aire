using System.Threading;
using System.Threading.Tasks;
using Aire.AppLayer.Abstractions;
using Aire.AppLayer.Providers;
using Aire.Domain.Providers;
using Aire.Providers;

namespace Aire.Services.Providers
{
    /// <summary>
    /// Transitional provider adapter that routes every provider type through the
    /// existing runtime gateway. This keeps behavior stable while the app
    /// migrates toward provider-specific adapters.
    /// </summary>
    public sealed class LegacyProviderAdapter : IProviderAdapter
    {
        private readonly IProviderRuntimeGateway _runtimeGateway;

        /// <summary>
        /// Creates the legacy adapter over the default runtime gateway.
        /// </summary>
        public LegacyProviderAdapter()
            : this(new ProviderRuntimeGateway())
        {
        }

        /// <summary>
        /// Creates the legacy adapter over an injected runtime gateway.
        /// </summary>
        /// <param name="runtimeGateway">Legacy runtime gateway used to keep provider behavior unchanged.</param>
        public LegacyProviderAdapter(IProviderRuntimeGateway runtimeGateway)
        {
            _runtimeGateway = runtimeGateway;
        }

        /// <inheritdoc />
        public string ProviderType => "*";

        /// <inheritdoc />
        public bool CanHandle(string providerType) => !string.IsNullOrWhiteSpace(providerType);

        /// <inheritdoc />
        public IAiProvider? BuildProvider(ProviderRuntimeRequest request)
            => _runtimeGateway.BuildProvider(request);

        /// <inheritdoc />
        public async Task<ProviderExecutionResult> ExecuteAsync(IAiProvider provider, ProviderRequestContext requestContext)
        {
            if (requestContext.EnabledToolCategories != null)
                provider.SetEnabledToolCategories(requestContext.EnabledToolCategories);

            var response = await provider.SendChatAsync(
                ProviderRequestContextMapper.ToLegacyMessages(requestContext.Messages),
                requestContext.CancellationToken).ConfigureAwait(false);

            return ProviderExecutionResultMapper.FromLegacyResponse(response);
        }

        /// <inheritdoc />
        public Task<ProviderSmokeTestResult> RunSmokeTestAsync(IAiProvider provider, CancellationToken cancellationToken)
            => _runtimeGateway.RunSmokeTestAsync(provider, cancellationToken);

        /// <inheritdoc />
        public async Task<ProviderValidationOutcome> ValidateAsync(IAiProvider provider, CancellationToken cancellationToken)
        {
            var result = await provider.ValidateConfigurationAsync(cancellationToken).ConfigureAwait(false);
            return ProviderValidationOutcomeMapper.FromLegacyResult(result);
        }
    }
}
