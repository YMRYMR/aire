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
    /// Dedicated adapter for the Claude Code local CLI provider.
    /// </summary>
    public sealed class ClaudeCodeAdapter : IProviderAdapter
    {
        /// <inheritdoc />
        public string ProviderType => "ClaudeCode";

        /// <inheritdoc />
        public bool CanHandle(string providerType)
            => string.Equals(providerType, ProviderType, StringComparison.OrdinalIgnoreCase);

        /// <inheritdoc />
        public IAiProvider? BuildProvider(ProviderRuntimeRequest request)
        {
            if (!CanHandle(request.Type))
                return null;

            var provider = new ClaudeCodeProvider();
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
            if (provider is ClaudeCodeProvider claudeCodeProvider)
            {
                var status = claudeCodeProvider.GetConnectionStatus();
                return status.IsInstalled
                    ? new ProviderSmokeTestResult(true)
                    : new ProviderSmokeTestResult(false, status.UserMessage);
            }

            var validation = await provider.ValidateConfigurationAsync(cancellationToken).ConfigureAwait(false);
            return validation.IsValid
                ? new ProviderSmokeTestResult(true)
                : new ProviderSmokeTestResult(false, validation.Error);
        }

        /// <inheritdoc />
        public async Task<ProviderValidationOutcome> ValidateAsync(IAiProvider provider, CancellationToken cancellationToken)
        {
            var result = await provider.ValidateConfigurationAsync(cancellationToken).ConfigureAwait(false);
            return ProviderValidationOutcomeMapper.FromLegacyResult(result);
        }
    }
}
