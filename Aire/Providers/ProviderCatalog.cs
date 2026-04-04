using System;
using System.Collections.Generic;
using Aire.Data;

namespace Aire.Providers
{
    /// <summary>
    /// Canonical provider descriptors used by UI, application services, and runtime creation.
    /// Keeps provider identity, defaults, and construction rules in one place.
    /// </summary>
    public static class ProviderCatalog
    {
        public sealed record ProviderDescriptor(
            string Type,
            string DisplayName,
            string DefaultName,
            string ApiKeyUrl,
            string SignUpUrl,
            bool RequiresApiKey,
            bool SupportsSessionCredential,
            Func<IAiProvider> CreateRuntimeProvider,
            Func<IProviderMetadata> CreateMetadataProvider);

        private static readonly IReadOnlyDictionary<string, ProviderDescriptor> Descriptors =
            new Dictionary<string, ProviderDescriptor>(StringComparer.OrdinalIgnoreCase)
            {
                ["OpenAI"] = CreateDescriptor("OpenAI", static () => new OpenAiProvider(), static () => new OpenAiProvider()),
                ["Groq"] = CreateDescriptor("Groq", static () => new GroqProvider(), static () => new GroqProvider()),
                ["OpenRouter"] = CreateDescriptor("OpenRouter", static () => new OpenRouterProvider(), static () => new OpenRouterProvider()),
                ["Codex"] = CreateDescriptor("Codex", static () => new CodexProvider(), static () => new CodexProvider()),
                ["Anthropic"] = CreateDescriptor("Anthropic", static () => new ClaudeAiProvider(), static () => new ClaudeAiProvider()),
                ["ClaudeWeb"] = CreateDescriptor("ClaudeWeb", static () => new ClaudeWebProvider(), static () => new ClaudeWebProvider()),
                ["GoogleAI"] = CreateDescriptor("GoogleAI", static () => new GoogleAiProvider(), static () => new GoogleAiProvider()),
                ["GoogleAIImage"] = CreateDescriptor("GoogleAIImage", static () => new GoogleAiImageProvider(), static () => new GoogleAiImageProvider()),
                ["DeepSeek"] = CreateDescriptor("DeepSeek", static () => new DeepSeekProvider(), static () => new DeepSeekProvider()),
                ["Inception"] = CreateDescriptor("Inception", static () => new InceptionProvider(), static () => new InceptionProvider()),
                ["Ollama"] = CreateDescriptor("Ollama", static () => new OllamaProvider(), static () => new PortableOllamaProvider()),
                ["Zai"] = CreateDescriptor("Zai", static () => new ZaiProvider(), static () => new ZaiProvider()),
            };

        public static IReadOnlyCollection<ProviderDescriptor> All => (IReadOnlyCollection<ProviderDescriptor>)Descriptors.Values;

        public static string NormalizeType(string? type)
            => ProviderIdentityCatalog.NormalizeType(type);

        public static ProviderDescriptor GetDescriptor(string type)
        {
            var normalizedType = NormalizeType(type);
            if (Descriptors.TryGetValue(normalizedType, out var descriptor))
                return descriptor;

            throw new NotSupportedException($"Provider type '{type}' is not supported.");
        }

        public static bool TryGetDescriptor(string type, out ProviderDescriptor descriptor)
        {
            try
            {
                descriptor = GetDescriptor(type);
                return true;
            }
            catch
            {
                descriptor = null!;
                return false;
            }
        }

        public static string GetDisplayName(string type)
            => ProviderIdentityCatalog.GetDisplayName(type);

        public static string GetDefaultName(string type)
            => ProviderIdentityCatalog.GetDefaultName(type);

        public static IAiProvider CreateRuntimeProvider(string type)
            => GetDescriptor(type).CreateRuntimeProvider();

        public static IProviderMetadata CreateMetadataProvider(string type)
            => GetDescriptor(type).CreateMetadataProvider();

        private static ProviderDescriptor CreateDescriptor(
            string type,
            Func<IAiProvider> createRuntimeProvider,
            Func<IProviderMetadata> createMetadataProvider)
        {
            var identity = ProviderIdentityCatalog.GetDescriptor(type);
            return new ProviderDescriptor(
                identity.Type,
                identity.DisplayName,
                identity.DefaultName,
                identity.ApiKeyUrl,
                identity.SignUpUrl,
                identity.RequiresApiKey,
                identity.SupportsSessionCredential,
                createRuntimeProvider,
                createMetadataProvider);
        }
    }
}
