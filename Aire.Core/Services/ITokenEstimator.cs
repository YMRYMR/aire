using System.Collections.Generic;
using Aire.Data;

namespace Aire.Services
{
    /// <summary>
    /// Provides token estimation for text, images, and attachments.
    /// </summary>
    public interface ITokenEstimator
    {
        /// <summary>
        /// Estimates the number of tokens that would be consumed by the given text for a specific model.
        /// </summary>
        /// <param name="text">The text to estimate.</param>
        /// <param name="modelId">Optional model identifier (e.g., "gpt-4-turbo"). If not provided, a default estimation method is used.</param>
        /// <returns>Estimated token count.</returns>
        int EstimateTokens(string text, string? modelId = null);

        /// <summary>
        /// Estimates the number of tokens that would be consumed by the given image.
        /// </summary>
        /// <param name="image">Metadata of the image.</param>
        /// <returns>Estimated token count.</returns>
        int EstimateTokensForImage(ImageMetadata image);

        /// <summary>
        /// Estimates the number of tokens that would be consumed by the given attachments.
        /// </summary>
        /// <param name="attachments">Collection of attachments.</param>
        /// <returns>Estimated token count.</returns>
        int EstimateTokensForAttachments(IEnumerable<MessageAttachment> attachments);
    }
}