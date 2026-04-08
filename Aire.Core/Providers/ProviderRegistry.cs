using Aire.Data;

namespace Aire.Providers
{
    /// <summary>
    /// Shared provider creation and metadata lookup logic.
    /// UI- or storage-specific factories can build on top of this.
    /// </summary>
    public static class ProviderRegistry
    {
        private const int MaxSupportedTimeoutMinutes = 35791;
        private static readonly Dictionary<string, IProviderMetadata> MetadataCache = new();

        /// <summary>
        /// Creates and initializes a provider instance for one persisted provider row.
        /// </summary>
        /// <param name="providerConfig">Persisted provider configuration loaded from the database.</param>
        /// <returns>An initialized provider ready to handle requests.</returns>
        public static IAiProvider CreateProvider(Provider providerConfig)
        {
            ArgumentNullException.ThrowIfNull(providerConfig);

            IAiProvider provider = ProviderIdentityCatalog.NormalizeType(providerConfig.Type) switch
            {
                "OpenAI"      => new OpenAiProvider(),
                "GoogleAI"    => new GoogleAiProvider(),
                "GoogleAIImage" => new GoogleAiImageProvider(),
                "DeepSeek"    => new DeepSeekProvider(),
                "Inception"   => new InceptionProvider(),
                "Codex"       => new CodexProvider(),
                "ClaudeCode"  => new ClaudeCodeProvider(),
                "Groq"        => new GroqProvider(),
                "OpenRouter"  => new OpenRouterProvider(),
                "Mistral"     => new MistralProvider(),
                "Ollama"      => new PortableOllamaProvider(),
                "Zai"         => new ZaiProvider(),
                _             => throw new NotSupportedException($"Provider type '{providerConfig.Type}' is not supported.")
            };

            provider.Initialize(BuildProviderConfig(providerConfig));
            return provider;
        }

        /// <summary>
        /// Normalizes a persisted provider row into the runtime configuration shape used by providers.
        /// </summary>
        /// <param name="providerConfig">Persisted provider configuration loaded from storage.</param>
        /// <returns>Runtime provider configuration with timeout clamping and model capabilities attached.</returns>
        public static ProviderConfig BuildProviderConfig(Provider providerConfig)
        {
            var allModels = ModelCatalog.GetDefaults(providerConfig.Type);
            var modelDef = allModels.FirstOrDefault(m =>
                string.Equals(m.Id, providerConfig.Model, StringComparison.OrdinalIgnoreCase));

            return new ProviderConfig
            {
                ApiKey = providerConfig.ApiKey ?? string.Empty,
                BaseUrl = providerConfig.BaseUrl,
                Model = providerConfig.Model,
                Temperature = 0.7,
                MaxTokens = 16384,
                TimeoutMinutes = Math.Clamp(providerConfig.TimeoutMinutes, 1, MaxSupportedTimeoutMinutes),
                ModelCapabilities = modelDef?.Capabilities
            };
        }

        /// <summary>
        /// Returns a metadata instance for the requested provider type, caching it for reuse.
        /// </summary>
        /// <param name="providerType">Provider type identifier, such as OpenAI, Groq, or Ollama.</param>
        /// <returns>A provider metadata implementation suitable for settings and onboarding.</returns>
        public static IProviderMetadata GetMetadata(string providerType)
        {
            var normalizedType = ProviderIdentityCatalog.NormalizeType(providerType);
            if (MetadataCache.TryGetValue(normalizedType, out var cached))
                return cached;

            IProviderMetadata meta = normalizedType switch
            {
                "OpenAI"     => new OpenAiProvider(),
                "GoogleAI"   => new GoogleAiProvider(),
                "GoogleAIImage" => new GoogleAiImageProvider(),
                "DeepSeek"   => new DeepSeekProvider(),
                "Inception"  => new InceptionProvider(),
                "Codex"      => new CodexProvider(),
                "ClaudeCode" => new ClaudeCodeProvider(),
                "Groq"       => new GroqProvider(),
                "OpenRouter" => new OpenRouterProvider(),
                "Mistral"    => new MistralProvider(),
                "Ollama"     => new PortableOllamaProvider(),
                "Zai"        => new ZaiProvider(),
                _            => throw new NotSupportedException($"Provider type '{providerType}' is not supported."),
            };

            MetadataCache[normalizedType] = meta;
            return meta;
        }
    }
}
