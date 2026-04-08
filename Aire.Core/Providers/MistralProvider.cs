namespace Aire.Providers
{
    /// <summary>
    /// Mistral AI provider using the OpenAI-compatible API surface.
    /// </summary>
    public class MistralProvider : OpenAiProvider
    {
        public override string ProviderType => "Mistral";
        public override string DisplayName => "Mistral AI";

        protected override ProviderCapabilities GetBaseCapabilities() =>
            ProviderCapabilities.TextChat |
            ProviderCapabilities.ImageInput |
            ProviderCapabilities.Streaming |
            ProviderCapabilities.ToolCalling |
            ProviderCapabilities.SystemPrompt;

        protected override string DefaultApiBaseUrl => "https://api.mistral.ai";

        protected override string[] ModelIdPrefixes => new[]
        {
            "mistral-",
            "ministral-",
            "magistral-",
            "devstral-",
            "codestral-",
            "voxtral-",
            "pixtral-",
            "open-mistral-",
            "open-mixtral-"
        };

        public override ProviderFieldHints FieldHints => new()
        {
            ShowBaseUrl = false
        };
    }
}
