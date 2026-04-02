using System;
using System.Threading;
using System.Threading.Tasks;
using Aire.AppLayer.Abstractions;
using Aire.AppLayer.Providers;
using Aire.Domain.Providers;
using Aire.Providers;

namespace Aire.Services.Providers
{
    /// <summary>
    /// First dedicated provider adapter implementation.
    /// Keeps Codex-specific provider construction and validation behind the
    /// application-facing adapter seam while the rest of the provider stack
    /// still falls through the legacy adapter.
    /// </summary>
    public sealed class CodexCliAdapter : IProviderAdapter
    {
        /// <inheritdoc />
        public string ProviderType => "Codex";

        /// <inheritdoc />
        public bool CanHandle(string providerType)
            => string.Equals(providerType, ProviderType, StringComparison.OrdinalIgnoreCase);

        /// <inheritdoc />
        public IAiProvider? BuildProvider(ProviderRuntimeRequest request)
        {
            if (!CanHandle(request.Type))
                return null;

            var provider = new CodexProvider();
            provider.Initialize(new ProviderConfig
            {
                ApiKey = request.ApiKey?.Trim() ?? string.Empty,
                BaseUrl = string.IsNullOrWhiteSpace(request.BaseUrl) ? null : request.BaseUrl.Trim(),
                Model = request.Model
            });

            return provider;
        }

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
        public async Task<ProviderSmokeTestResult> RunSmokeTestAsync(IAiProvider provider, CancellationToken cancellationToken)
        {
            var response = await provider.SendChatAsync([new ChatMessage { Role = "user", Content = "Reply with exactly OK and nothing else." }], cancellationToken)
                .ConfigureAwait(false);

            return response.IsSuccess
                ? new ProviderSmokeTestResult(true)
                : new ProviderSmokeTestResult(false, response.ErrorMessage);
        }

        /// <inheritdoc />
        public async Task<ProviderValidationOutcome> ValidateAsync(IAiProvider provider, CancellationToken cancellationToken)
        {
            var result = await provider.ValidateConfigurationAsync(cancellationToken).ConfigureAwait(false);
            return ProviderValidationOutcomeMapper.FromLegacyResult(result);
        }
    }
}
