using System.Threading.Tasks;
using Aire.AppLayer.Abstractions;
using Aire.Domain.Providers;
using Aire.Providers;
using Aire.Services.Providers;

namespace Aire.AppLayer.Providers
{
    /// <summary>
    /// Application-layer completion flow for the onboarding provider-setup step.
    /// </summary>
    public sealed class OnboardingProviderSetupApplicationService
    {
        /// <summary>
        /// Normalized onboarding step-three input gathered from the wizard UI.
        /// </summary>
        public sealed record Step3Request(
            string ProviderName,
            string ProviderType,
            string? ApiKey,
            string? BaseUrl,
            string? StandardModel,
            string? OllamaModel,
            bool ClaudeWebSessionReady);

        /// <summary>
        /// Result of handling the onboarding provider-setup step.
        /// </summary>
        public sealed record Step3Result(
            bool ShouldAdvance,
            bool SavedProvider,
            bool IsDuplicate,
            string ProviderType,
            string Model);

        private readonly ProviderSetupApplicationService _providerSetupService = new();

        /// <summary>
        /// Applies onboarding provider rules, performs duplicate-aware persistence for credentialed and
        /// credentialless local providers, and tells the UI whether the wizard should advance.
        /// </summary>
        /// <param name="providerRepository">Provider repository used for duplicate detection and insert.</param>
        /// <param name="request">Normalized step-three wizard values.</param>
        /// <returns>Whether the wizard should advance and whether a provider was saved or rejected as duplicate.</returns>
        public async Task<Step3Result> CompleteStepAsync(IProviderRepository providerRepository, Step3Request request)
        {
            var type = string.IsNullOrWhiteSpace(request.ProviderType) ? "OpenAI" : request.ProviderType;
            var name = request.ProviderName?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(name))
                return new Step3Result(true, false, false, type, string.Empty);

            var model = type == "Ollama"
                ? request.OllamaModel?.Trim() ?? string.Empty
                : request.StandardModel?.Trim() ?? string.Empty;

            bool claudeWebSession = type == "ClaudeWeb" && request.ClaudeWebSessionReady;
            bool credentiallessProvider = ProviderIdentityCatalog.TryGetDescriptor(type, out ProviderIdentityCatalog.ProviderIdentityDescriptor? descriptor)
                && !descriptor.RequiresApiKey
                && !descriptor.SupportsSessionCredential;
            bool hasCredential = credentiallessProvider
                || claudeWebSession
                || !string.IsNullOrWhiteSpace(request.ApiKey);

            var effectiveApiKey = string.IsNullOrWhiteSpace(request.ApiKey) && claudeWebSession
                ? "claude.ai-session"
                : (string.IsNullOrWhiteSpace(request.ApiKey) ? null : request.ApiKey);

            if (!hasCredential)
                return new Step3Result(true, false, false, type, model);

            var persistResult = await _providerSetupService.SaveNewProviderAsync(
                providerRepository,
                new ProviderDraft(
                    name,
                    type,
                    effectiveApiKey,
                    string.IsNullOrWhiteSpace(request.BaseUrl) ? null : request.BaseUrl.Trim(),
                    model));

            if (persistResult.IsDuplicate)
                return new Step3Result(false, false, true, type, model);

            return new Step3Result(true, persistResult.Saved, false, type, model);
        }
    }
}
