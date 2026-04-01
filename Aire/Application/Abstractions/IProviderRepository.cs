using Aire.Data;

namespace Aire.AppLayer.Abstractions
{
    /// <summary>
    /// Persistence boundary for configured AI providers.
    /// </summary>
    public interface IProviderRepository
    {
        Task<List<Provider>> GetProvidersAsync();
        Task UpdateProviderAsync(Provider provider);
        Task SaveProviderOrderAsync(IEnumerable<Provider> orderedProviders);
        Task<int> InsertProviderAsync(Provider provider);
        Task DeleteProviderAsync(int id);
    }
}
