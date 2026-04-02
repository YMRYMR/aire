using System.Collections.Generic;
using Aire.AppLayer.Abstractions;
using Aire.Services.Providers;

namespace Aire.AppLayer.Providers
{
    /// <summary>
    /// Supplies the default provider-adapter registrations used until individual
    /// providers are migrated to dedicated adapters.
    /// </summary>
    public static class ProviderAdapterDefaults
    {
        /// <summary>
        /// Creates the default provider-adapter set for the application layer.
        /// </summary>
        public static IReadOnlyList<IProviderAdapter> CreateDefaultAdapters()
            => new IProviderAdapter[]
            {
                new CodexCliAdapter(),
                new OpenAiCompatibleAdapter(),
                new GoogleAiAdapter(),
                new AnthropicAdapter(),
                new ClaudeWebAdapter(),
                new OllamaAdapter(),
                new LegacyProviderAdapter()
            };
    }
}
