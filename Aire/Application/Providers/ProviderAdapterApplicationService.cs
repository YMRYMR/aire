using System;
using System.Collections.Generic;
using System.Linq;
using Aire.AppLayer.Abstractions;

namespace Aire.AppLayer.Providers
{
    /// <summary>
    /// Resolves the application-facing provider adapter for a provider type.
    /// This is the first seam for moving the app away from shared workflow code
    /// that knows too much about individual provider quirks.
    /// </summary>
    public sealed class ProviderAdapterApplicationService
    {
        private readonly IReadOnlyList<IProviderAdapter> _adapters;

        /// <summary>
        /// Creates the adapter resolver over the supplied provider adapters.
        /// </summary>
        /// <param name="adapters">Available provider adapters registered by the app.</param>
        public ProviderAdapterApplicationService(IEnumerable<IProviderAdapter> adapters)
        {
            _adapters = adapters?.ToList() ?? throw new ArgumentNullException(nameof(adapters));
        }

        /// <summary>
        /// Returns the registered adapter for a provider type.
        /// </summary>
        /// <param name="providerType">Persisted provider type identifier.</param>
        /// <returns>The adapter that should handle that provider type.</returns>
        /// <exception cref="InvalidOperationException">Thrown when no adapter is registered for the type.</exception>
        public IProviderAdapter Resolve(string providerType)
        {
            return TryResolve(providerType)
                ?? throw new InvalidOperationException($"No provider adapter is registered for '{providerType}'.");
        }

        /// <summary>
        /// Tries to find the registered adapter for a provider type.
        /// </summary>
        /// <param name="providerType">Persisted provider type identifier.</param>
        /// <returns>The matching adapter, or <see langword="null"/> when no adapter is registered.</returns>
        public IProviderAdapter? TryResolve(string providerType)
        {
            if (string.IsNullOrWhiteSpace(providerType))
                return null;

            return _adapters.FirstOrDefault(adapter => adapter.CanHandle(providerType));
        }
    }
}
