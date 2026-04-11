using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Aire.Data;
using LLMSharp.Anthropic.Tokenizer;

namespace Aire.Services
{
    /// <summary>
    /// Token estimator for Anthropic (Claude) models.
    /// Uses a character‑based heuristic (chars / 4) as a fallback; a proper tokenizer can be integrated later.
    /// </summary>
    public sealed class AnthropicTokenEstimator : ITokenEstimator
    {
        private const double DefaultCharsPerToken = 4.0;
        private static readonly ConcurrentDictionary<string, ClaudeTokenizer> TokenizerCache = new();

        /// <inheritdoc />
        public int EstimateTokens(string text, string? modelId = null)
        {
            if (string.IsNullOrEmpty(text))
                return 0;

            try
            {
                var tokenizer = GetTokenizer(modelId);
                return tokenizer.CountTokens(text);
            }
            catch
            {
                // If tokenizer fails, fall back to character‑based heuristic.
                return FallbackCharacterEstimation(text);
            }
        }

        private static ClaudeTokenizer GetTokenizer(string? modelId)
        {
            string model = modelId ?? "claude";
            return TokenizerCache.GetOrAdd(model, _ => new ClaudeTokenizer());
        }

        private static int FallbackCharacterEstimation(string text)
        {
            double tokenCount = text.Length / DefaultCharsPerToken;
            return Math.Max(1, (int)Math.Ceiling(tokenCount));
        }

        /// <inheritdoc />
        public int EstimateTokensForImage(ImageMetadata image)
        {
            if (image == null)
                return 0;

            // Anthropic's vision models (Claude 3+) treat images as a fixed token overhead.
            // The exact formula is not public; we use a conservative placeholder.
            // Assume low‑detail image cost similar to OpenAI's low‑detail (85 tokens).
            return 85;
        }

        /// <inheritdoc />
        public int EstimateTokensForAttachments(IEnumerable<MessageAttachment> attachments)
        {
            if (attachments == null)
                return 0;

            // Anthropic does not support generic attachments beyond images.
            // Estimate tokens for file names and MIME types.
            int totalChars = attachments.Sum(a =>
                (a.FileName?.Length ?? 0) +
                (a.MimeType?.Length ?? 0) +
                (a.FilePath?.Length ?? 0));

            if (totalChars == 0)
                return 0;

            double tokenCount = totalChars / DefaultCharsPerToken;
            return Math.Max(1, (int)Math.Ceiling(tokenCount));
        }
    }
}