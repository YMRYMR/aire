using System;
using System.Collections.Generic;
using Aire.Providers;

namespace Aire.Services
{
    /// <summary>
    /// Selects the appropriate token estimator based on provider type and/or model identifier.
    /// </summary>
    public sealed class TokenEstimatorRegistry
    {
        private readonly IReadOnlyDictionary<string, ITokenEstimator> _providerEstimators;
        private readonly ITokenEstimator _defaultEstimator;

        /// <summary>
        /// Initializes a new instance of the <see cref="TokenEstimatorRegistry"/> class with the built‑in mappings.
        /// </summary>
        public TokenEstimatorRegistry() : this(CreateDefaultMappings()) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="TokenEstimatorRegistry"/> class with custom mappings.
        /// </summary>
        /// <param name="providerEstimators">
        /// A dictionary mapping provider types (e.g., "OpenAI") to token estimator instances.
        /// </param>
        /// <param name="defaultEstimator">
        /// The estimator to use when a provider type is not found in the dictionary.
        /// If null, a <see cref="CharacterTokenEstimator"/> is used.
        /// </param>
        public TokenEstimatorRegistry(
            IReadOnlyDictionary<string, ITokenEstimator> providerEstimators,
            ITokenEstimator? defaultEstimator = null)
        {
            _providerEstimators = providerEstimators ?? throw new ArgumentNullException(nameof(providerEstimators));
            _defaultEstimator = defaultEstimator ?? new CharacterTokenEstimator();
        }

        /// <summary>
        /// Gets the token estimator for the specified provider type.
        /// </summary>
        /// <param name="providerType">The provider type (e.g., "OpenAI", "Anthropic", "GoogleAI").</param>
        /// <param name="modelId">Optional model identifier; currently unused but reserved for future per‑model adjustments.</param>
        /// <returns>An <see cref="ITokenEstimator"/> suitable for the provider.</returns>
        public ITokenEstimator GetEstimator(string providerType, string? modelId = null)
        {
            if (string.IsNullOrEmpty(providerType))
                return _defaultEstimator;

            var normalizedType = ProviderIdentityCatalog.NormalizeType(providerType);
            if (_providerEstimators.TryGetValue(normalizedType, out var estimator))
                return estimator;

            // Fall back to default estimator (character‑based).
            return _defaultEstimator;
        }

        /// <summary>
        /// Creates the default provider‑to‑estimator mappings used by the parameterless constructor.
        /// </summary>
        private static IReadOnlyDictionary<string, ITokenEstimator> CreateDefaultMappings()
        {
            var mappings = new Dictionary<string, ITokenEstimator>(StringComparer.OrdinalIgnoreCase)
            {
                ["OpenAI"] = new OpenAiTokenEstimator(),
                ["Groq"] = new OpenAiTokenEstimator(), // Groq uses OpenAI‑compatible tokenization
                ["OpenRouter"] = new OpenAiTokenEstimator(), // OpenRouter also OpenAI‑compatible
                ["Mistral"] = new OpenAiTokenEstimator(), // Mistral uses similar tokenization
                ["Anthropic"] = new AnthropicTokenEstimator(),
                ["ClaudeCode"] = new AnthropicTokenEstimator(),
                ["ClaudeWeb"] = new AnthropicTokenEstimator(),
                ["GoogleAI"] = new GoogleTokenEstimator(),
                ["GoogleAIImage"] = new GoogleTokenEstimator(),
                ["DeepSeek"] = new OpenAiTokenEstimator(), // DeepSeek uses OpenAI‑compatible tokenization
                ["Inception"] = new OpenAiTokenEstimator(),
                ["Zai"] = new OpenAiTokenEstimator(),
                // Ollama models vary; default to character‑based estimator.
                // Codex is OpenAI‑based but uses different tokenization; we treat as OpenAI.
                ["Codex"] = new OpenAiTokenEstimator(),
            };

            return mappings;
        }
    }
}