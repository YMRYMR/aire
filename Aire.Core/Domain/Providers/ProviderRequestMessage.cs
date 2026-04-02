namespace Aire.Domain.Providers
{
    /// <summary>
    /// Provider-independent message shape used by shared provider-semantic contracts.
    /// This keeps domain-layer request planning separate from the legacy runtime
    /// provider message type used by current transport implementations.
    /// </summary>
    public sealed class ProviderRequestMessage
    {
        /// <summary>
        /// Logical message role, such as <c>system</c>, <c>user</c>, or <c>assistant</c>.
        /// </summary>
        public string Role { get; init; } = string.Empty;

        /// <summary>
        /// Text content for the message.
        /// </summary>
        public string Content { get; init; } = string.Empty;

        /// <summary>
        /// Optional local image path associated with the message.
        /// </summary>
        public string? ImagePath { get; init; }

        /// <summary>
        /// Optional inline image bytes for providers that support binary attachments.
        /// </summary>
        public byte[]? ImageBytes { get; init; }

        /// <summary>
        /// MIME type for <see cref="ImageBytes"/> when binary image content is supplied.
        /// </summary>
        public string? ImageMimeType { get; init; }

        /// <summary>
        /// Whether this message is part of a stable prefix that a provider may cache when supported.
        /// </summary>
        public bool PreferPromptCache { get; init; }
    }
}
