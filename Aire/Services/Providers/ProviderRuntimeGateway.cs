using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Aire.AppLayer.Abstractions;
using Aire.Domain.Providers;
using Aire.Providers;
using ProviderChatMessage = Aire.Providers.ChatMessage;

namespace Aire.Services.Providers
{
    /// <summary>
    /// Default infrastructure adapter for provider runtime creation and smoke testing.
    /// </summary>
    public sealed class ProviderRuntimeGateway : IProviderRuntimeGateway
    {
        private readonly ProviderConfigurationWorkflowService _configurationWorkflow = new();

        /// <inheritdoc />
        public IAiProvider? BuildProvider(ProviderRuntimeRequest request)
            => _configurationWorkflow.CreateRuntimeProvider(request);

        /// <inheritdoc />
        public async Task<ProviderSmokeTestResult> RunSmokeTestAsync(IAiProvider provider, CancellationToken cancellationToken)
        {
            var messages = new List<ProviderChatMessage> { new() { Role = "user", Content = "Hi" } };
            var response = await provider.SendChatAsync(messages, cancellationToken);
            return response.IsSuccess
                ? new ProviderSmokeTestResult(true)
                : new ProviderSmokeTestResult(false, response.ErrorMessage);
        }
    }
}
