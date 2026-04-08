using System;
using System.Collections.Generic;
using System.Linq;

namespace Aire.Providers
{
    /// <summary>
    /// Canonical provider identity metadata shared across core, application, and UI layers.
    /// Runtime provider construction lives outside this catalog so Core does not depend on app code.
    /// </summary>
    public static class ProviderIdentityCatalog
    {
        public sealed record ProviderIdentityDescriptor(
            string Type,
            string DisplayName,
            string DefaultName,
            string ApiKeyUrl,
            string SignUpUrl,
            bool RequiresApiKey,
            bool SupportsSessionCredential);

        private static readonly IReadOnlyDictionary<string, ProviderIdentityDescriptor> Descriptors =
            new Dictionary<string, ProviderIdentityDescriptor>(StringComparer.OrdinalIgnoreCase)
            {
                ["OpenAI"] = new(
                    "OpenAI",
                    "OpenAI",
                    "OpenAI",
                    "https://platform.openai.com/api-keys",
                    "https://platform.openai.com/signup",
                    RequiresApiKey: true,
                    SupportsSessionCredential: false),
                ["Groq"] = new(
                    "Groq",
                    "Groq",
                    "Groq",
                    "https://console.groq.com/keys",
                    "https://console.groq.com/",
                    RequiresApiKey: true,
                    SupportsSessionCredential: false),
                ["OpenRouter"] = new(
                    "OpenRouter",
                    "OpenRouter",
                    "OpenRouter",
                    "https://openrouter.ai/keys",
                    "https://openrouter.ai/",
                    RequiresApiKey: true,
                    SupportsSessionCredential: false),
                ["Mistral"] = new(
                    "Mistral",
                    "Mistral AI",
                    "Mistral AI",
                    "https://console.mistral.ai/api-keys",
                    "https://console.mistral.ai/",
                    RequiresApiKey: true,
                    SupportsSessionCredential: false),
                ["Codex"] = new(
                    "Codex",
                    "Codex",
                    "Codex",
                    string.Empty,
                    "https://openai.com/codex/",
                    RequiresApiKey: false,
                    SupportsSessionCredential: false),
                ["ClaudeCode"] = new(
                    "ClaudeCode",
                    "Claude Code",
                    "Claude Code",
                    string.Empty,
                    "https://docs.anthropic.com/en/docs/claude-code",
                    RequiresApiKey: false,
                    SupportsSessionCredential: false),
                ["Anthropic"] = new(
                    "Anthropic",
                    "Anthropic API",
                    "Anthropic API",
                    "https://console.anthropic.com/settings/keys",
                    "https://console.anthropic.com/",
                    RequiresApiKey: true,
                    SupportsSessionCredential: false),
                ["ClaudeWeb"] = new(
                    "ClaudeWeb",
                    "Claude.ai",
                    "Claude.ai",
                    string.Empty,
                    "https://claude.ai/",
                    RequiresApiKey: false,
                    SupportsSessionCredential: true),
                ["GoogleAI"] = new(
                    "GoogleAI",
                    "Google AI",
                    "Google AI",
                    "https://aistudio.google.com/app/apikey",
                    "https://aistudio.google.com/",
                    RequiresApiKey: true,
                    SupportsSessionCredential: false),
                ["GoogleAIImage"] = new(
                    "GoogleAIImage",
                    "Google AI Images",
                    "Google AI Images",
                    "https://aistudio.google.com/app/apikey",
                    "https://aistudio.google.com/",
                    RequiresApiKey: true,
                    SupportsSessionCredential: false),
                ["DeepSeek"] = new(
                    "DeepSeek",
                    "DeepSeek",
                    "DeepSeek",
                    "https://platform.deepseek.com/api_keys",
                    "https://platform.deepseek.com/",
                    RequiresApiKey: true,
                    SupportsSessionCredential: false),
                ["Inception"] = new(
                    "Inception",
                    "Inception",
                    "Inception",
                    "https://platform.inceptionlabs.ai/",
                    "https://platform.inceptionlabs.ai/",
                    RequiresApiKey: true,
                    SupportsSessionCredential: false),
                ["Ollama"] = new(
                    "Ollama",
                    "Ollama",
                    "Ollama",
                    string.Empty,
                    "https://ollama.com/",
                    RequiresApiKey: false,
                    SupportsSessionCredential: false),
                ["Zai"] = new(
                    "Zai",
                    "Zhipu AI (z.ai)",
                    "z.ai",
                    "https://www.bigmodel.cn/usercenter/apikeys",
                    "https://www.bigmodel.cn/",
                    RequiresApiKey: true,
                    SupportsSessionCredential: false),
            };

        public static IReadOnlyCollection<ProviderIdentityDescriptor> All =>
            ProviderVisibility.ShowClaudeWebProvider
                ? (IReadOnlyCollection<ProviderIdentityDescriptor>)Descriptors.Values
                : Descriptors.Values.Where(descriptor => !ProviderVisibility.IsHiddenFromRelease(descriptor.Type)).ToArray();

        public static string NormalizeType(string? type)
        {
            var trimmed = (type ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
                throw new InvalidOperationException("Provider type is required.");

            return trimmed.ToLowerInvariant() switch
            {
                "openai" => "OpenAI",
                "codex" => "Codex",
                "claudecode" => "ClaudeCode",
                "claude code" => "ClaudeCode",
                "claude-code" => "ClaudeCode",
                "anthropic" => "Anthropic",
                "claudeweb" => "ClaudeWeb",
                "claude.ai" => "ClaudeWeb",
                "googleai" => "GoogleAI",
                "google ai" => "GoogleAI",
                "googleaiimage" => "GoogleAIImage",
                "google ai images" => "GoogleAIImage",
                "deepseek" => "DeepSeek",
                "inception" => "Inception",
                "groq" => "Groq",
                "openrouter" => "OpenRouter",
                "mistral" => "Mistral",
                "mistralai" => "Mistral",
                "mistral ai" => "Mistral",
                "mistral-ai" => "Mistral",
                "ollama" => "Ollama",
                "zai" => "Zai",
                "z.ai" => "Zai",
                "chatgptweb" => "OpenAI",
                "chatgpt" => "OpenAI",
                _ => trimmed
            };
        }

        public static ProviderIdentityDescriptor GetDescriptor(string type)
        {
            var normalizedType = NormalizeType(type);
            if (Descriptors.TryGetValue(normalizedType, out var descriptor))
                return descriptor;

            throw new NotSupportedException($"Provider type '{type}' is not supported.");
        }

        public static bool TryGetDescriptor(string type, out ProviderIdentityDescriptor descriptor)
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
            => TryGetDescriptor(type, out var descriptor) ? descriptor.DisplayName : type;

        public static string GetDefaultName(string type)
            => TryGetDescriptor(type, out var descriptor) ? descriptor.DefaultName : type;
    }
}
