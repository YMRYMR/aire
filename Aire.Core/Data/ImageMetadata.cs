using System;

namespace Aire.Data
{
    /// <summary>
    /// Represents metadata about an image that can be used for token estimation.
    /// </summary>
    public sealed record ImageMetadata
    {
        /// <summary>
        /// Gets the width of the image in pixels.
        /// </summary>
        public int Width { get; init; }

        /// <summary>
        /// Gets the height of the image in pixels.
        /// </summary>
        public int Height { get; init; }

        /// <summary>
        /// Gets the image format (e.g., "png", "jpeg", "gif", "webp").
        /// </summary>
        public string Format { get; init; } = string.Empty;

        /// <summary>
        /// Gets the size of the image file in bytes.
        /// </summary>
        public long SizeBytes { get; init; }

        /// <summary>
        /// Gets the optional file path or URL of the image.
        /// </summary>
        public string? FilePath { get; init; }

        /// <summary>
        /// Gets the optional detail level (e.g., "high", "low") used by some AI providers.
        /// </summary>
        public string? DetailLevel { get; init; }

        /// <summary>
        /// Creates a new instance of <see cref="ImageMetadata"/>.
        /// </summary>
        public ImageMetadata() { }

        /// <summary>
        /// Creates a new instance of <see cref="ImageMetadata"/> with the specified values.
        /// </summary>
        public ImageMetadata(int width, int height, string format, long sizeBytes, string? filePath = null, string? detailLevel = null)
        {
            Width = width;
            Height = height;
            Format = format ?? throw new ArgumentNullException(nameof(format));
            SizeBytes = sizeBytes;
            FilePath = filePath;
            DetailLevel = detailLevel;
        }
    }
}