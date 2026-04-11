using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Aire.Data;
using Microsoft.ML.Tokenizers;

namespace Aire.Services
{
    /// <summary>
    /// Token estimator for OpenAI models, using Tiktoken for text and the official vision token formula for images.
    /// </summary>
    public sealed class OpenAiTokenEstimator : ITokenEstimator
    {
        private static readonly ConcurrentDictionary<string, Tokenizer> EncodingCache = new();

        /// <inheritdoc />
        public int EstimateTokens(string text, string? modelId = null)
        {
            if (string.IsNullOrEmpty(text))
                return 0;

            try
            {
                var encoding = GetEncoding(modelId);
                return encoding.CountTokens(text);
            }
            catch
            {
                // If Microsoft.ML.Tokenizers fails, fall back to character‑based heuristic.
                return FallbackCharacterEstimation(text);
            }
        }

        private static Tokenizer GetEncoding(string? modelId)
        {
            string model = modelId ?? "gpt-4";
            return EncodingCache.GetOrAdd(model, _ =>
            {
                try
                {
                    return TiktokenTokenizer.CreateForModel(model);
                }
                catch
                {
                    // If model not recognized, fall back to cl100k_base which works for most recent models.
                    return TiktokenTokenizer.CreateForEncoding("cl100k_base");
                }
            });
        }

        /// <inheritdoc />
        public int EstimateTokensForImage(ImageMetadata image)
        {
            if (image == null)
                return 0;

            // Apply OpenAI's vision token formula:
            // - Low‑detail images: fixed 85 tokens.
            // - High‑detail images: 170 tokens per 512×512 tile, rounded up.
            // Reference: https://platform.openai.com/docs/guides/vision
            bool isHighDetail = string.Equals(image.DetailLevel, "high", StringComparison.OrdinalIgnoreCase) ||
                                (image.DetailLevel == null && image.Width > 0 && image.Height > 0); // assume high detail if dimensions present

            if (!isHighDetail)
                return 85;

            // Compute number of 512×512 tiles.
            int tilesWide = (int)Math.Ceiling(image.Width / 512.0);
            int tilesHigh = (int)Math.Ceiling(image.Height / 512.0);
            int totalTiles = tilesWide * tilesHigh;

            return totalTiles * 170;
        }

        /// <inheritdoc />
        public int EstimateTokensForAttachments(IEnumerable<MessageAttachment> attachments)
        {
            if (attachments == null)
                return 0;

            // OpenAI does not charge extra tokens for non‑image attachments beyond their textual representation.
            // We sum token counts of file names and MIME types as a conservative estimate.
            int totalTokens = 0;
            foreach (var attachment in attachments)
            {
                if (attachment.IsImage)
                {
                    // For image attachments we could extract metadata, but we don't have dimensions here.
                    // Use a conservative default (low‑detail image).
                    totalTokens += 85;
                }
                else
                {
                    // Textual metadata.
                    string?[] parts = { attachment.FileName, attachment.MimeType, attachment.FilePath };
                    foreach (var part in parts)
                        if (!string.IsNullOrEmpty(part))
                            totalTokens += FallbackCharacterEstimation(part);
                }
            }

            return totalTokens;
        }

        private static int FallbackCharacterEstimation(string text)
        {
            // Character‑based heuristic (≈ tokens = chars / 4).
            const double charsPerToken = 4.0;
            double tokenCount = text.Length / charsPerToken;
            return Math.Max(1, (int)Math.Ceiling(tokenCount));
        }
    }
}