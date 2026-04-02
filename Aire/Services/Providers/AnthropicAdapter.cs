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
    /// Dedicated adapter for the direct Anthropic API provider.
    /// </summary>
    public sealed class AnthropicAdapter : IProviderAdapter
    {
        private readonly ProviderConfigurationWorkflowService _configurationWorkflow = new();

        /// <inheritdoc />
        public string ProviderType => "Anthropic";

        /// <inheritdoc />
        public bool CanHandle(string providerType)
            => string.Equals(providerType, ProviderType, StringComparison.OrdinalIgnoreCase);

        /// <inheritdoc />
        public IAiProvider? BuildProvider(ProviderRuntimeRequest request)
        {
            if (!CanHandle(request.Type))
                return null;

            return _configurationWorkflow.CreateRuntimeProvider(request);
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
