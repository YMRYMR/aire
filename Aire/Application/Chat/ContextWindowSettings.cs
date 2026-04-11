using System.Collections.Generic;

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
        int SummaryMaxCharacters,
        int? MaxTokens = null,
        int AnchorTokens = 0,
        int TailTokens = 0,
        bool EnableTokenAwareTruncation = false,
        bool EnableToolFocusWindow = false,
        bool EnableRetryFollowUpWindow = false,
        bool EnableGradualCompaction = false,
        int CompactionTokenThreshold = 0,
        IReadOnlyDictionary<string, int>? PerMessageTypeLimits = null)
    {
        public static readonly ContextWindowSettings Default = new(
            MaxMessages: 40,
            AnchorMessages: 6,
            UncachedRecentMessages: 12,
            EnablePromptCaching: true,
            EnableConversationSummaries: true,
            SummaryMaxCharacters: 900,
            MaxTokens: null,
            AnchorTokens: 0,
            TailTokens: 0,
            EnableTokenAwareTruncation: false,
            EnableToolFocusWindow: false,
            EnableRetryFollowUpWindow: false,
            EnableGradualCompaction: false,
            CompactionTokenThreshold: 0,
            PerMessageTypeLimits: null);
    }
}
