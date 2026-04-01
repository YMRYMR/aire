using System.Collections.Generic;
using System.Threading.Tasks;
using Aire.AppLayer.Abstractions;
using Aire.Data;
using Aire.Services.Providers;

namespace Aire.AppLayer.Providers
{
    /// <summary>
    /// Application-layer persistence workflow for saving provider editor changes from settings.
    /// </summary>
    public sealed class ProviderEditorSaveApplicationService
    {
        /// <summary>
        /// View-neutral editor values that should be applied to a provider before persisting.
        /// </summary>
        public sealed record SaveRequest(
            Provider Provider,
            string Name,
            string Type,
            string? ApiKey,
            string? BaseUrl,
            string RawModelText,
            string? SelectedModelValue,
            int TimeoutMinutes,
            bool IsEnabled,
            IEnumerable<(string DisplayName, string ModelName)>? KnownModelMappings);

        private readonly ProviderConfigurationWorkflowService _configurationWorkflow = new();

        /// <summary>
        /// Applies editor values to the provider and persists the updated record.
        /// </summary>
        /// <param name="request">Current provider plus the normalized editor values to apply.</param>
        /// <param name="providerRepository">Provider repository used to persist the updated record.</param>
        public async Task SaveAsync(SaveRequest request, IProviderRepository providerRepository)
        {
            _configurationWorkflow.ApplyProviderEditorValues(
                request.Provider,
                request.Name,
                request.Type,
                request.ApiKey,
                request.BaseUrl,
                request.RawModelText,
                request.SelectedModelValue,
                request.TimeoutMinutes,
                request.IsEnabled,
                request.KnownModelMappings);

            await providerRepository.UpdateProviderAsync(request.Provider);
        }
    }
}
