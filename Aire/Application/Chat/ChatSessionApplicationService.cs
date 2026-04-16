using Aire.AppLayer.Abstractions;
using Aire.Data;
using System.Collections.Generic;

namespace Aire.AppLayer.Chat
{
    /// <summary>
    /// Application-layer use cases for the active chat session and its persisted conversation state.
    /// </summary>
    public sealed class ChatSessionApplicationService
    {
        private readonly IConversationRepository _conversations;
        private readonly ISettingsRepository _settings;

        /// <summary>
        /// Creates the chat-session service over repository abstractions rather than concrete SQLite types.
        /// </summary>
        /// <param name="conversations">Conversation/message persistence port.</param>
        /// <param name="settings">Settings persistence port.</param>
        public ChatSessionApplicationService(IConversationRepository conversations, ISettingsRepository settings)
        {
            _conversations = conversations;
            _settings = settings;
        }

        /// <summary>
        /// Persists the currently selected provider id for the next launch.
        /// </summary>
        public Task SaveSelectedProviderAsync(int providerId)
            => _settings.SetSettingAsync("SelectedProviderId", providerId.ToString());

        /// <summary>
        /// Loads the previously selected provider id when one was persisted.
        /// </summary>
        /// <returns>The persisted provider id, or <see langword="null"/> when no valid value was found.</returns>
        public async Task<int?> GetSelectedProviderIdAsync()
        {
            var raw = await _settings.GetSettingAsync("SelectedProviderId");
            return int.TryParse(raw, out int id) ? id : null;
        }

        /// <summary>
        /// Loads the latest conversation for one provider, if it has previous history.
        /// </summary>
        public Task<Conversation?> GetLatestConversationAsync(int providerId)
            => _conversations.GetLatestConversationAsync(providerId);

        /// <summary>
        /// Reassigns an existing conversation to a newly selected provider.
        /// </summary>
        public Task UpdateConversationProviderAsync(int conversationId, int providerId)
            => _conversations.UpdateConversationProviderAsync(conversationId, providerId);

        /// <summary>
        /// Persists a user turn and optionally applies the generated first-message title for the conversation.
        /// </summary>
        public async Task PersistUserMessageAsync(
            int conversationId,
            string content,
            string? imagePath,
            IEnumerable<MessageAttachment>? attachments,
            string? suggestedConversationTitle)
        {
            await _conversations.SaveMessageAsync(conversationId, "user", content, imagePath, attachments);
            if (!string.IsNullOrWhiteSpace(suggestedConversationTitle))
                await _conversations.UpdateConversationTitleAsync(conversationId, suggestedConversationTitle);
        }

        /// <summary>
        /// Persists an assistant text turn.
        /// </summary>
        public Task PersistAssistantMessageAsync(
            int conversationId,
            string content,
            string? imagePath = null,
            IEnumerable<MessageAttachment>? attachments = null,
            int? tokens = null)
            => _conversations.SaveMessageAsync(conversationId, "assistant", content, imagePath, attachments, tokens);

        /// <summary>
        /// Persists a tool status line such as approval or denial.
        /// </summary>
        public Task PersistToolStatusAsync(int conversationId, string status)
            => _conversations.SaveMessageAsync(conversationId, "tool", status);

        /// <summary>
        /// Persists a system/status line that should appear in the transcript but not in provider history.
        /// </summary>
        public Task PersistSystemMessageAsync(int conversationId, string content)
            => _conversations.SaveMessageAsync(conversationId, "system", content);

        /// <summary>
        /// Persists orchestrator narration so it can be restored with its dedicated UI treatment.
        /// </summary>
        public Task PersistOrchestratorMessageAsync(int conversationId, string content)
            => _conversations.SaveMessageAsync(conversationId, "orchestrator", content);

        /// <summary>
        /// Loads an arbitrary application setting by key. Prefer typed methods where available.
        /// </summary>
        public Task<string?> GetSettingAsync(string key)
            => _settings.GetSettingAsync(key);
    }
}
