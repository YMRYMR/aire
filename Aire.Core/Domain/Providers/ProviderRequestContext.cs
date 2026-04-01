using System.Collections.Generic;
using System.Threading;

namespace Aire.Domain.Providers
{
    /// <summary>
    /// Normalized input for a single provider execution turn.
    /// The application layer builds this from conversation state, tool configuration,
    /// and user settings; adapters translate it into provider-specific payloads.
    /// </summary>
    public sealed class ProviderRequestContext
    {
        /// <summary>
        /// Ordered conversation messages to send to the provider.
        /// </summary>
        public IReadOnlyList<ProviderRequestMessage> Messages { get; init; } = [];

        /// <summary>
        /// Model identifier to use for this turn (e.g. "gpt-4o-mini", "glm-4-flash").
        /// </summary>
        public string Model { get; init; } = string.Empty;

        /// <summary>
        /// Tool categories enabled for this turn.
        /// Empty means no tools; <see langword="null"/> means use the provider/model default.
        /// </summary>
        public IReadOnlyList<string>? EnabledToolCategories { get; init; }

        /// <summary>
        /// System prompt constructed by the application layer, including tool instructions
        /// and model-switch metadata when applicable.
        /// May be <see langword="null"/> when the provider handles system prompts differently
        /// or when the system prompt is already embedded in <see cref="Messages"/>.
        /// </summary>
        public string? SystemPrompt { get; init; }

        /// <summary>
        /// Temperature override for this turn.
        /// Zero or negative means use the provider/model default.
        /// </summary>
        public double Temperature { get; init; }

        /// <summary>
        /// Maximum tokens for the provider response.
        /// Zero or negative means use the provider/model default.
        /// </summary>
        public int MaxTokens { get; init; }

        /// <summary>
        /// Cancellation token for the provider call.
        /// </summary>
        public CancellationToken CancellationToken { get; init; }
    }
}
