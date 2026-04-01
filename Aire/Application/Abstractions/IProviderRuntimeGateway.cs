using System.Threading;
using System.Threading.Tasks;
using Aire.Domain.Providers;
using Aire.Providers;
using Aire.Services.Providers;

namespace Aire.AppLayer.Abstractions
{
    /// <summary>
    /// Application-layer port for constructing and lightly probing provider runtimes.
    /// </summary>
    public interface IProviderRuntimeGateway
    {
        /// <summary>
        /// Builds a configured provider instance from normalized provider form values.
        /// </summary>
        IAiProvider? BuildProvider(ProviderRuntimeRequest request);

        /// <summary>
        /// Runs a lightweight connectivity probe against a configured provider.
        /// </summary>
        Task<ProviderSmokeTestResult> RunSmokeTestAsync(IAiProvider provider, CancellationToken cancellationToken);
    }
}
