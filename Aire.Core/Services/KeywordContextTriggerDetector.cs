using System;
using System.Collections.Generic;
using System.Linq;
using Aire.Data;

namespace Aire.Services
{
    /// <summary>
    /// A simple trigger detector that scans message content for tool‑related keywords and retry phrases.
    /// </summary>
    public sealed class KeywordContextTriggerDetector : IContextTriggerDetector
    {
        private static readonly HashSet<string> ToolKeywords = new(StringComparer.OrdinalIgnoreCase)
        {
            "using a tool",
            "list files",
            "run command",
            "execute",
            "tool",
            "function",
            "call",
            "invoke",
            "api",
            "command",
            "script",
            "toolcall",
            "tool call"
        };

        private static readonly HashSet<string> RetryPhrases = new(StringComparer.OrdinalIgnoreCase)
        {
            "try again",
            "continue",
            "retry",
            "again",
            "still",
            "didn't work",
            "doesn't work",
            "failed",
            "error",
            "please fix",
            "can you",
            "could you",
            "once more"
        };

        /// <inheritdoc />
        public ContextTriggerDetection DetectTriggers(IReadOnlyList<Message> recentMessages)
        {
            if (recentMessages == null || recentMessages.Count == 0)
                return ContextTriggerDetection.None;

            // Examine the most recent message (and optionally previous ones).
            var latest = recentMessages[^1];
            bool isToolFocus = ContainsAnyKeyword(latest.Content, ToolKeywords);
            bool isRetryFollowUp = ContainsAnyKeyword(latest.Content, RetryPhrases);

            // If the latest message doesn't show tool focus, look at earlier messages
            // to see if there was a tool call recently.
            if (!isToolFocus && recentMessages.Count >= 2)
            {
                // Check previous message for tool keywords.
                var previous = recentMessages[^2];
                isToolFocus = ContainsAnyKeyword(previous.Content, ToolKeywords);
            }

            return new ContextTriggerDetection(isToolFocus, isRetryFollowUp);
        }

        private static bool ContainsAnyKeyword(string text, HashSet<string> keywords)
        {
            if (string.IsNullOrWhiteSpace(text))
                return false;

            // Simple substring match; for production a more sophisticated token‑based
            // approach could be used.
            foreach (var keyword in keywords)
            {
                if (text.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }
    }
}