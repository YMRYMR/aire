using System;
using System.Linq;
using System.Threading.Tasks;
using Aire.AppLayer.Abstractions;
using Aire.Data;
using Aire.Domain.Providers;
using Aire.Providers;
using Aire.Services.Providers;

namespace Aire.AppLayer.Providers
{
    /// <summary>
    /// Owns provider creation workflow shared across non-UI entry points.
    /// </summary>
    public sealed class ProviderCreationApplicationService
    {
        public sealed record ProviderCreationRequest(
            string? Name,
            string Type,
            string? ApiKey,
            string? BaseUrl,
            string Model,
            bool IsEnabled,
            string? Color,
            int? InheritCredentialsFromProviderId = null);

        public sealed record ProviderCreationResult(
            Provider Provider,
            bool IsDuplicate);

        private readonly ProviderConfigurationWorkflowService _configurationWorkflow = new();

        public async Task<ProviderCreationResult> CreateAsync(IProviderRepository providerRepository, ProviderCreationRequest request)
        {
            var descriptor = ProviderCatalog.GetDescriptor(request.Type);
            var existingProviders = await providerRepository.GetProvidersAsync();

            string? apiKey = string.IsNullOrWhiteSpace(request.ApiKey) ? null : request.ApiKey.Trim();
            string? baseUrl = string.IsNullOrWhiteSpace(request.BaseUrl) ? null : request.BaseUrl.Trim();

            if (request.InheritCredentialsFromProviderId.HasValue &&
                (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(baseUrl)))
            {
                var source = existingProviders.FirstOrDefault(p => p.Id == request.InheritCredentialsFromProviderId.Value)
                    ?? throw new InvalidOperationException("Credential source provider was not found.");

                if (string.IsNullOrWhiteSpace(apiKey))
                    apiKey = source.ApiKey;
                if (string.IsNullOrWhiteSpace(baseUrl))
                    baseUrl = source.BaseUrl;
            }

            var draft = new ProviderDraft(
                string.IsNullOrWhiteSpace(request.Name) ? descriptor.DefaultName : request.Name.Trim(),
                descriptor.Type,
                apiKey,
                baseUrl,
                request.Model.Trim(),
                request.IsEnabled,
                string.IsNullOrWhiteSpace(request.Color) ? "#007ACC" : request.Color.Trim());

            var persistResult = await _configurationWorkflow.SaveNewProviderAsync(providerRepository, draft);
            if (!persistResult.Saved)
                return new ProviderCreationResult(new Provider { Type = descriptor.Type, Model = draft.Model, Name = draft.Name }, persistResult.IsDuplicate);

            var createdProvider = (await providerRepository.GetProvidersAsync()).FirstOrDefault(p => p.Id == persistResult.ProviderId)
                ?? throw new InvalidOperationException("Created provider could not be reloaded.");

            return new ProviderCreationResult(createdProvider, false);
        }
    }
}
