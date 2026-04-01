using System;
using Aire.Domain.Providers;

namespace Aire.AppLayer.Abstractions
{
    /// <summary>
    /// Normalized result returned by a provider adapter after executing one turn.
    /// Adapters decode provider-specific responses into this shared shape
    /// so that application workflows operate on uniform semantics.
    /// </summary>
    /// <remarks>
    /// This type lives in the Application layer rather than Domain because its exact
    /// shape will be refined as adapters are implemented and wired in. Once the adapter
    /// boundary is stable, it may be promoted to Domain.
    /// </remarks>
    public sealed class ProviderExecutionResult
    {
        /// <summary>
        /// Whether the provider call completed without a transport or API error.
        /// <see langword="false"/> does not necessarily mean a workflow error —
        /// check <see cref="Intent"/> for the actual workflow outcome.
        /// </summary>
        public bool IsSuccess { get; init; }

        /// <summary>
        /// The high-level workflow intent decoded from the provider response.
        /// Always set when <see cref="IsSuccess"/> is <see langword="true"/>.
        /// </summary>
        public WorkflowIntent? Intent { get; init; }

        /// <summary>
        /// Raw text returned by the provider before intent parsing.
        /// Useful for streaming and for adapters that need to return partial results.
        /// </summary>
        public string RawContent { get; init; } = string.Empty;

        /// <summary>
        /// Total tokens consumed by the provider call, when reported.
        /// Zero means the provider did not report token usage.
        /// </summary>
        public int TokensUsed { get; init; }

        /// <summary>
        /// Wall-clock duration of the provider call.
        /// </summary>
        public TimeSpan Duration { get; init; }

        /// <summary>
        /// Human-readable error text when <see cref="IsSuccess"/> is <see langword="false"/>.
        /// This is the provider-facing error, not the workflow error —
        /// the <see cref="Intent"/> may still be an <see cref="WorkflowIntentKind.Error"/>
        /// with a different user-facing message.
        /// </summary>
        public string? ErrorMessage { get; init; }

        /// <summary>Convenience factory for a successful execution with an intent.</summary>
        public static ProviderExecutionResult Succeeded(
            WorkflowIntent intent,
            string rawContent = "",
            int tokensUsed = 0,
            TimeSpan duration = default) => new()
        {
            IsSuccess = true,
            Intent = intent,
            RawContent = rawContent,
            TokensUsed = tokensUsed,
            Duration = duration
        };

        /// <summary>Convenience factory for a failed execution.</summary>
        public static ProviderExecutionResult Failed(
            string errorMessage,
            TimeSpan duration = default) => new()
        {
            IsSuccess = false,
            ErrorMessage = errorMessage,
            Duration = duration
        };
    }
}
