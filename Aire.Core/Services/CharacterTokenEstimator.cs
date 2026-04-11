using System;
using System.Collections.Generic;
using System.Linq;
using Aire.Data;

namespace Aire.Services
{
    /// <summary>
    /// A generic token estimator that uses a simple character‑based heuristic (≈ tokens = chars / 4).
    /// Suitable as a fallback when provider‑specific tokenization is unavailable.
    /// </summary>
    public sealed class CharacterTokenEstimator : ITokenEstimator
    {
        /// <summary>
        /// Default average characters per token used by the heuristic.
        /// </summary>
        public const double DefaultCharsPerToken = 4.0;

        private readonly double _charsPerToken;

        /// <summary>
        /// Initializes a new instance of the <see cref="CharacterTokenEstimator"/> class with the default
        /// characters‑per‑token ratio (4.0).
        /// </summary>
        public CharacterTokenEstimator() : this(DefaultCharsPerToken) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="CharacterTokenEstimator"/> class with a custom ratio.
        /// </summary>
        /// <param name="charsPerToken">Average number of characters per token (must be greater than zero).</param>
        public CharacterTokenEstimator(double charsPerToken)
        {
            if (charsPerToken <= 0)
                throw new ArgumentOutOfRangeException(nameof(charsPerToken), "Characters per token must be greater than zero.");

            _charsPerToken = charsPerToken;
        }

        /// <inheritdoc />
        public int EstimateTokens(string text, string? modelId = null)
        {
            if (string.IsNullOrEmpty(text))
                return 0;

            // Count characters (including whitespace) and apply the heuristic.
            double tokenCount = text.Length / _charsPerToken;
            return Math.Max(1, (int)Math.Ceiling(tokenCount));
        }

        /// <inheritdoc />
        public int EstimateTokensForImage(ImageMetadata image)
        {
            // No reliable generic heuristic for images; return a conservative placeholder.
            // In a real implementation this would consider image dimensions, detail level, etc.
            return 0;
        }

        /// <inheritdoc />
        public int EstimateTokensForAttachments(IEnumerable<MessageAttachment> attachments)
        {
            if (attachments == null)
                return 0;

            // For generic attachments we assume they are represented by their file names and maybe metadata.
            // A simple heuristic: count characters in file names and MIME types.
            int totalChars = attachments.Sum(a =>
                (a.FileName?.Length ?? 0) +
                (a.MimeType?.Length ?? 0) +
                (a.FilePath?.Length ?? 0));

            if (totalChars == 0)
                return 0;

            double tokenCount = totalChars / _charsPerToken;
            return Math.Max(1, (int)Math.Ceiling(tokenCount));
        }
    }
}