using Aire.Domain.Providers;

namespace Aire.Providers
{
    /// <summary>
    /// Dedicated Google image-generation provider that reuses Gemini's native image-generation API
    /// while keeping image-focused models separate from normal chat-oriented Google providers.
    /// </summary>
    public sealed class GoogleAiImageProvider : GoogleAiProvider
    {
        public override string ProviderType => "GoogleAIImage";
        public override string DisplayName => "Google AI Images";

        protected override ProviderCapabilities GetBaseCapabilities() =>
            ProviderCapabilities.TextChat |
            ProviderCapabilities.ImageInput |
            ProviderCapabilities.SystemPrompt;
    }
}
