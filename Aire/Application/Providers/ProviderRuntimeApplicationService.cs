using System.Threading;
using System.Threading.Tasks;
using Aire.AppLayer.Abstractions;
using Aire.Domain.Providers;
using Aire.Providers;
using Aire.Services.Providers;

namespace Aire.AppLayer.Providers
{
    /// <summary>
    /// Shared application-layer workflow for building runtime providers from form state and running lightweight connection tests.
    /// </summary>
    public sealed class ProviderRuntimeApplicationService
    {
        private readonly ProviderAdapterApplicationService _adapterService;

        /// <summary>
        /// Creates the provider runtime workflow over the default infrastructure gateway.
        /// </summary>
        public ProviderRuntimeApplicationService()
            : this(new ProviderAdapterApplicationService(ProviderAdapterDefaults.CreateDefaultAdapters()))
        {
        }

        /// <summary>
        /// Creates the provider runtime workflow over an injected adapter resolver.
        /// </summary>
        /// <param name="adapterService">Application-side resolver for provider adapters.</param>
        public ProviderRuntimeApplicationService(ProviderAdapterApplicationService adapterService)
        {
            _adapterService = adapterService;
        }

        /// <summary>
        /// Creates the provider runtime workflow over one legacy runtime gateway.
        /// This overload keeps the current tests and callers stable while the adapter
        /// architecture is introduced incrementally.
        /// </summary>
        /// <param name="runtimeGateway">Infrastructure adapter for provider construction and smoke tests.</param>
        public ProviderRuntimeApplicationService(IProviderRuntimeGateway runtimeGateway)
            : this(new ProviderAdapterApplicationService(new IProviderAdapter[]
            {
                new ProviderRuntimeGatewayAdapter(runtimeGateway)
            }))
        {
        }

        /// <summary>
        /// Builds a configured provider instance from normalized form state.
        /// </summary>
        /// <param name="request">Normalized provider form values.</param>
        /// <returns>A provider ready to use for testing or capability probing, or <see langword="null"/> when the form is incomplete.</returns>
        public IAiProvider? BuildProvider(ProviderRuntimeRequest request)
            => _adapterService.Resolve(request.Type).BuildProvider(request);

        /// <summary>
        /// Runs a minimal chat request to verify that the provider can accept the supplied configuration.
        /// </summary>
        /// <param name="provider">Configured provider instance to test.</param>
        /// <param name="cancellationToken">Cancellation token for the probe request.</param>
        /// <returns>A success flag and optional error text describing the failure.</returns>
        public Task<ProviderSmokeTestResult> RunSmokeTestAsync(IAiProvider provider, CancellationToken cancellationToken)
            => _adapterService.Resolve(provider.ProviderType).RunSmokeTestAsync(provider, cancellationToken);

        private sealed class ProviderRuntimeGatewayAdapter : IProviderAdapter
        {
            private readonly IProviderRuntimeGateway _runtimeGateway;

            public ProviderRuntimeGatewayAdapter(IProviderRuntimeGateway runtimeGateway)
            {
                _runtimeGateway = runtimeGateway;
            }

            public string ProviderType => "*";

            public bool CanHandle(string providerType) => !string.IsNullOrWhiteSpace(providerType);

            public IAiProvider? BuildProvider(ProviderRuntimeRequest request)
                => _runtimeGateway.BuildProvider(request);

            public Task<ProviderSmokeTestResult> RunSmokeTestAsync(IAiProvider provider, CancellationToken cancellationToken)
                => _runtimeGateway.RunSmokeTestAsync(provider, cancellationToken);
        }
    }
}
