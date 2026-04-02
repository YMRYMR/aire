using Aire.AppLayer.Abstractions;
using Aire.Data;

namespace Aire.AppLayer.Chat
{
    /// <summary>
    /// Application-layer use cases for conversation lifecycle and message retrieval.
    /// </summary>
    public sealed class ConversationApplicationService
    {
        private readonly IConversationRepository _conversations;

        /// <summary>
        /// Creates the conversation application service over the persistence boundary.
        /// </summary>
        /// <param name="conversations">Conversation and message persistence port.</param>
        public ConversationApplicationService(IConversationRepository conversations)
        {
            _conversations = conversations;
        }

        /// <summary>
        /// Loads all messages that belong to one conversation.
        /// </summary>
        public Task<List<Aire.Data.Message>> GetMessagesAsync(int conversationId)
            => _conversations.GetMessagesAsync(conversationId);

        /// <summary>
        /// Creates a new conversation for one provider and returns its id.
        /// </summary>
        public Task<int> CreateConversationAsync(int providerId, string title)
            => _conversations.CreateConversationAsync(providerId, title);

        /// <summary>
        /// Lists conversation summaries, optionally filtered by search text.
        /// </summary>
        public Task<List<ConversationSummary>> ListConversationsAsync(string? search = null)
            => _conversations.ListConversationsAsync(search);

        /// <summary>
        /// Renames one conversation.
        /// </summary>
        public Task RenameConversationAsync(int conversationId, string title)
            => _conversations.UpdateConversationTitleAsync(conversationId, title);

        /// <summary>
        /// Updates the stored assistant mode for one conversation.
        /// </summary>
        public Task UpdateConversationAssistantModeAsync(int conversationId, string assistantModeKey)
            => _conversations.UpdateConversationAssistantModeAsync(conversationId, assistantModeKey);

        /// <summary>
        /// Deletes one conversation and all of its stored messages.
        /// </summary>
        public async Task DeleteConversationAsync(int conversationId)
        {
            await _conversations.DeleteMessagesByConversationIdAsync(conversationId);
            await _conversations.DeleteConversationAsync(conversationId);
        }

        /// <summary>
        /// Deletes every conversation and message in storage.
        /// </summary>
        public Task DeleteAllConversationsAsync()
            => _conversations.DeleteAllConversationsAsync();

        /// <summary>
        /// Returns one conversation by id, or null when it does not exist.
        /// </summary>
        public Task<Conversation?> GetConversationAsync(int conversationId)
            => _conversations.GetConversationAsync(conversationId);
    }
}
