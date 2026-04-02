namespace Aire.AppLayer.Chat
{
    /// <summary>
    /// Configures how much conversation history Aire keeps in the active provider context
    /// and how much of that retained prefix is marked as cache-friendly.
    /// </summary>
    public sealed record ContextWindowSettings(
        int MaxMessages,
        int AnchorMessages,
        int UncachedRecentMessages,
        bool EnablePromptCaching,
        bool EnableConversationSummaries,
        int SummaryMaxCharacters)
    {
        public static readonly ContextWindowSettings Default = new(
            MaxMessages: 40,
            AnchorMessages: 6,
            UncachedRecentMessages: 12,
            EnablePromptCaching: true,
            EnableConversationSummaries: true,
            SummaryMaxCharacters: 900);
    }
}
