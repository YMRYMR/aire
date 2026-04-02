using System.Collections.Generic;
using System.Linq;
using Aire.Domain.Providers;
using Aire.Providers;

namespace Aire.AppLayer.Providers
{
    /// <summary>
    /// Bridges the new shared provider-semantic request contracts to the legacy
    /// runtime provider message shape used by the current transport implementations.
    /// This keeps adapter migration incremental while the provider stack is being refactored.
    /// </summary>
    public static class ProviderRequestContextMapper
    {
        /// <summary>
        /// Converts legacy runtime messages into the shared provider request message type.
        /// </summary>
        /// <param name="messages">Legacy provider-runtime messages.</param>
        /// <returns>Shared provider request messages with the same ordered content.</returns>
        public static IReadOnlyList<ProviderRequestMessage> FromLegacyMessages(IEnumerable<ChatMessage> messages)
        {
            return messages?.Select(message => new ProviderRequestMessage
            {
                Role = message.Role,
                Content = message.Content,
                ImagePath = message.ImagePath,
                ImageBytes = message.ImageBytes,
                ImageMimeType = message.ImageMimeType,
                PreferPromptCache = message.PreferPromptCache
            }).ToList() ?? [];
        }

        /// <summary>
        /// Converts provider-independent request messages into the legacy runtime message type.
        /// </summary>
        /// <param name="messages">Shared request messages from the semantic provider contract.</param>
        /// <returns>Legacy provider-runtime messages with the same ordered content.</returns>
        public static IReadOnlyList<ChatMessage> ToLegacyMessages(IEnumerable<ProviderRequestMessage> messages)
        {
            return messages?.Select(message => new ChatMessage
            {
                Role = message.Role,
                Content = message.Content,
                ImagePath = message.ImagePath,
                ImageBytes = message.ImageBytes,
                ImageMimeType = message.ImageMimeType,
                PreferPromptCache = message.PreferPromptCache
            }).ToList() ?? [];
        }
    }
}
