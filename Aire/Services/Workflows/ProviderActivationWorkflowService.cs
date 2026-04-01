namespace Aire.Services.Workflows
{
    /// <summary>
    /// Decides how the main chat session should react when the active provider changes.
    /// </summary>
    public sealed class ProviderActivationWorkflowService
    {
        public enum ConversationActionKind
        {
            KeepCurrentConversation,
            LoadExistingConversation,
            CreateNewConversation,
        }

        public sealed record ProviderActivationPlan(
            bool ProviderChanged,
            bool ShouldAnnounceSwitch,
            ConversationActionKind ConversationAction,
            int? ConversationIdToLoad,
            string? NewConversationTitle = null,
            string? NewConversationMessage = null);

        /// <summary>
        /// Builds the provider-activation plan after a new provider is selected.
        /// </summary>
        /// <param name="previousProviderId">Previously active provider id, if any.</param>
        /// <param name="selectedProviderId">Provider id that was just selected.</param>
        /// <param name="currentConversationId">Currently active conversation id, if any.</param>
        /// <param name="latestConversation">Latest stored conversation for the selected provider, if one exists.</param>
        /// <param name="providerName">Display name of the selected provider.</param>
        /// <param name="showSwitchedMessage">Whether the caller wants provider-switch announcements.</param>
        /// <returns>The conversation action and announcement behavior the UI should execute.</returns>
        public ProviderActivationPlan BuildPlan(
            int? previousProviderId,
            int selectedProviderId,
            int? currentConversationId,
            Aire.Data.Conversation? latestConversation,
            string providerName,
            bool showSwitchedMessage)
        {
            bool providerChanged = previousProviderId != selectedProviderId;
            bool hasCurrentConversation = currentConversationId.HasValue;

            if (hasCurrentConversation)
            {
                return new ProviderActivationPlan(
                    providerChanged,
                    providerChanged && showSwitchedMessage,
                    ConversationActionKind.KeepCurrentConversation,
                    currentConversationId);
            }

            if (latestConversation != null)
            {
                return new ProviderActivationPlan(
                    providerChanged,
                    false,
                    ConversationActionKind.LoadExistingConversation,
                    latestConversation.Id);
            }

            return new ProviderActivationPlan(
                providerChanged,
                false,
                ConversationActionKind.CreateNewConversation,
                null,
                "Chat",
                $"New conversation started with {providerName}.");
        }
    }
}
