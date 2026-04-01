using System;
using System.IO;
using System.Threading.Tasks;
using Aire.AppLayer.Abstractions;

namespace Aire.AppLayer.Chat
{
    /// <summary>
    /// Handles conversation-scoped asset persistence such as screenshots saved into transcript storage.
    /// </summary>
    public sealed class ConversationAssetApplicationService
    {
        private readonly IConversationRepository _conversations;

        /// <summary>
        /// Creates the asset application service over the conversation persistence boundary.
        /// </summary>
        public ConversationAssetApplicationService(IConversationRepository conversations)
        {
            _conversations = conversations;
        }

        /// <summary>
        /// Copies a screenshot into the conversation asset folder and persists it in transcript history.
        /// </summary>
        public async Task<string> PersistScreenshotAsync(int? conversationId, string screenshotPath, string screenshotsRootFolder, DateTime now)
        {
            if (!conversationId.HasValue)
                return screenshotPath;

            var folder = Path.Combine(screenshotsRootFolder, conversationId.Value.ToString());
            Directory.CreateDirectory(folder);
            var filename = $"{now:yyyyMMdd_HHmmss_fff}.png";
            var persistedPath = Path.Combine(folder, filename);
            File.Copy(screenshotPath, persistedPath, overwrite: true);
            await _conversations.SaveMessageAsync(conversationId.Value, "assistant", string.Empty, persistedPath);
            return persistedPath;
        }
    }
}
