using System;
using Aire.Data;
using Aire.Domain.Providers;
using OpenAI;
using OpenAI.Managers;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Aire.Services;

namespace Aire.Providers
{
    /// <summary>
    /// Zhipu AI (z.ai) provider - OpenAI-compatible API.
    /// Documentation: https://docs.z.ai/api-reference/introduction
    /// </summary>
    public class ZaiProvider : OpenAiProvider
    {
        public override string ProviderType => "Zai";
        public override string DisplayName => "Zhipu AI (z.ai)";

        protected override string DefaultApiBaseUrl => "https://api.z.ai/api/coding/paas/v4";
        protected override string[] ModelIdPrefixes => new[] { "glm-" };

        public override ProviderFieldHints FieldHints => new() { ShowBaseUrl = true };

        protected override ProviderCapabilities GetBaseCapabilities() =>
            ProviderCapabilities.TextChat |
            ProviderCapabilities.Streaming |
            ProviderCapabilities.ToolCalling |
            ProviderCapabilities.ImageInput |
            ProviderCapabilities.SystemPrompt;

        /// <summary>
        /// z.ai's API version is part of the base path (/api/paas/v4), so the
        /// Betalgo SDK must use it as the ApiVersion directly without appending /v1
        /// (which SplitSdkUrl would do).  This produces the correct endpoint:
        ///   https://api.z.ai/api/paas/v4/chat/completions
        /// </summary>
        public override void Initialize(ProviderConfig config)
        {
            base.Initialize(config);

            if (string.IsNullOrWhiteSpace(Config.ApiKey))
                throw new ArgumentException($"API key is required for {DisplayName} provider.");

            var baseUrl = !string.IsNullOrWhiteSpace(Config.BaseUrl)
                ? Config.BaseUrl.TrimEnd('/')
                : DefaultApiBaseUrl;

            var uri = new Uri(baseUrl);
            var host = $"{uri.Scheme}://{uri.Host}"
                     + (uri.IsDefaultPort ? "" : $":{uri.Port}");

            var options = new OpenAiOptions
            {
                ApiKey      = Config.ApiKey,
                BaseDomain  = host,
                ApiVersion  = uri.AbsolutePath.Trim('/')
            };

            _openAiService = new OpenAIService(options);
        }

        /// <summary>
        /// z.ai models and chat endpoints omit the /v1/ segment because the
        /// version is already embedded in the base path (/api/paas/v4).
        /// </summary>
        protected override string BuildModelsUrl(string baseUrl) => $"{baseUrl}/models";

        protected override string BuildChatCompletionsUrl(string baseUrl) => $"{baseUrl}/chat/completions";

        protected override string BuildImageGenerationUrl(string baseUrl)
        {
            var normalized = baseUrl.TrimEnd('/');
            const string codingPath = "/api/coding/paas/v4";
            const string imagePath = "/api/paas/v4";

            if (normalized.Contains(codingPath, StringComparison.OrdinalIgnoreCase))
                return normalized.Replace(codingPath, imagePath, StringComparison.OrdinalIgnoreCase) + "/images/generations";

            return $"{normalized}/images/generations";
        }

        public override async Task<AiResponse> SendChatAsync(
            IEnumerable<ChatMessage> messages,
            CancellationToken cancellationToken = default)
        {
            var response = await base.SendChatAsync(messages, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccess)
                response.ErrorMessage = NormalizeZaiError(response.ErrorMessage);
            return response;
        }

        public override async Task<ProviderValidationResult> ValidateConfigurationAsync(CancellationToken cancellationToken = default)
        {
            var result = await base.ValidateConfigurationAsync(cancellationToken).ConfigureAwait(false);
            return result.IsValid
                ? result
                : ProviderValidationResult.Fail(NormalizeZaiError(result.Error));
        }

        public static string NormalizeZaiError(string? message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return "Unknown error from z.ai.";

            var original = message;
            var readable = ProviderErrorClassifier.ExtractReadableMessage(message);
            if (!string.IsNullOrWhiteSpace(readable))
                message = readable;

            if (message.Contains("Insufficient balance or no resource package", StringComparison.OrdinalIgnoreCase))
            {
                return "z.ai rejected the request because this account does not currently have access to that model. " +
                       "If your account only has a GLM Coding Plan, use the coding base URL https://api.z.ai/api/coding/paas/v4 and a coding-plan model such as GLM-5.1, GLM-4.7, or GLM-4.5 Air. Otherwise add normal z.ai balance/resource access for GLM-5.1.";
            }

            if (message.Contains("OpenAI request failed.", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("OpenAI configuration validation failed.", StringComparison.OrdinalIgnoreCase))
            {
                return "An unexpected error occurred while processing your request through z.ai.";
            }

            var looksZaiSpecific =
                message.Contains("z.ai", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("glm-", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("resource package", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("billing", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("balance", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("credit", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("quota", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("payment", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("rate limit", StringComparison.OrdinalIgnoreCase);

            if (readable is null && !looksZaiSpecific &&
                !original.Contains("z.ai", StringComparison.OrdinalIgnoreCase) &&
                !original.Contains("glm-", StringComparison.OrdinalIgnoreCase))
            {
                return "An unexpected error occurred while processing your request through z.ai.";
            }

            return message;
        }
    }
}
