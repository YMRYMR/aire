using System.Threading;
using System.Threading.Tasks;

namespace Aire.Providers
{
    /// <summary>
    /// Optional capability for providers that can generate raster images from text prompts.
    /// </summary>
    public interface IImageGenerationProvider
    {
        bool SupportsImageGeneration { get; }

        Task<ImageGenerationResult> GenerateImageAsync(string prompt, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Normalized image-generation result returned by a provider.
    /// </summary>
    public sealed class ImageGenerationResult
    {
        public bool IsSuccess { get; set; }
        public byte[]? ImageBytes { get; set; }
        public string ImageMimeType { get; set; } = "image/png";
        public string? RevisedPrompt { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
