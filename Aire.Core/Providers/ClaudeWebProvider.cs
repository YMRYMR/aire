using Aire.Data;
using Aire.Domain.Providers;

namespace Aire.Providers
{
    internal class ClaudeWebProvider : BaseAiProvider
    {
        public override string ProviderType => "ClaudeWeb";
        public override string DisplayName => "Claude.ai";

        protected override ProviderCapabilities GetBaseCapabilities() =>
            ProviderCapabilities.TextChat |
            ProviderCapabilities.Streaming |
            ProviderCapabilities.ImageInput |
            ProviderCapabilities.ToolCalling |
            ProviderCapabilities.SystemPrompt;

        public override ProviderFieldHints FieldHints => new()
        {
            ShowApiKey = false,
            ApiKeyRequired = false,
            ShowBaseUrl = false
        };

        public override List<ModelDefinition> GetDefaultModels() => ModelCatalog.GetDefaults("Anthropic");

        public override Task<AiResponse> SendChatAsync(IEnumerable<ChatMessage> messages, CancellationToken cancellationToken = default)
            => Task.FromResult(new AiResponse
            {
                IsSuccess = false,
                ErrorMessage = "ClaudeWeb is only available in the desktop app."
            });

        public override Task<ProviderValidationResult> ValidateConfigurationAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(ProviderValidationResult.Fail("Claude.ai Web does not support API key validation."));
    }
}
