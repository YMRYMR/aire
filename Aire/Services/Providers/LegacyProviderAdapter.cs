using System.Threading;
using System.Threading.Tasks;
using Aire.AppLayer.Abstractions;
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
        public Task<ProviderSmokeTestResult> RunSmokeTestAsync(IAiProvider provider, CancellationToken cancellationToken)
            => _runtimeGateway.RunSmokeTestAsync(provider, cancellationToken);
    }
}
