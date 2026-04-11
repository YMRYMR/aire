using System;
using System.Collections.Generic;
using System.Linq;
using Aire.Data;

namespace Aire.Services
{
    /// <summary>
    /// Token estimator for Google AI (Gemini) models.
    /// Uses a character‑based heuristic (chars / 4) as a fallback.
    /// </summary>
    public sealed class GoogleTokenEstimator : ITokenEstimator
    {
        private const double DefaultCharsPerToken = 4.0;

        /// <inheritdoc />
        public int EstimateTokens(string text, string? modelId = null)
        {
            if (string.IsNullOrEmpty(text))
                return 0;

            // Google's tokenization is similar to OpenAI's but uses a different vocabulary.
            // We approximate with character count.
            // TODO: Integrate Google's tokenizer if a .NET library becomes available.
            double tokenCount = text.Length / DefaultCharsPerToken;
            return Math.Max(1, (int)Math.Ceiling(tokenCount));
        }

        /// <inheritdoc />
        public int EstimateTokensForImage(ImageMetadata image)
        {
            if (image == null)
                return 0;

            // Google's Gemini vision models treat images as a fixed token overhead.
            // The exact formula is not public; we use a conservative placeholder.
            // Assume low‑detail image cost similar to OpenAI's low‑detail (85 tokens).
            return 85;
        }

        /// <inheritdoc />
        public int EstimateTokensForAttachments(IEnumerable<MessageAttachment> attachments)
        {
            if (attachments == null)
                return 0;

            // Google does not support generic attachments beyond images.
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