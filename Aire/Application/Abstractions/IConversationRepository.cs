using Aire.Data;

namespace Aire.AppLayer.Abstractions
{
    /// <summary>
    /// Persistence boundary for conversations and chat messages.
    /// </summary>
    public interface IConversationRepository
    {
        Task<int> CreateConversationAsync(int providerId, string title);
        Task<Conversation?> GetLatestConversationAsync(int providerId);
        Task<Conversation?> GetConversationAsync(int conversationId);
        Task<List<ConversationSummary>> ListConversationsAsync(string? search = null);
        Task UpdateConversationTitleAsync(int conversationId, string title);
        Task UpdateConversationProviderAsync(int conversationId, int providerId);
        Task UpdateConversationAssistantModeAsync(int conversationId, string assistantModeKey);
        Task SaveMessageAsync(int conversationId, string role, string content, string? imagePath = null);
        Task<List<Aire.Data.Message>> GetMessagesAsync(int conversationId);
        Task DeleteMessagesByConversationIdAsync(int conversationId);
        Task DeleteConversationAsync(int conversationId);
        Task DeleteAllConversationsAsync();
    }
}
