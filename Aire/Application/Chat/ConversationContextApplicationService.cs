using System;
using System.Collections.Generic;
using System.Linq;
using Aire.Providers;

namespace Aire.AppLayer.Chat
{
    /// <summary>
    /// Builds a bounded conversation window and marks the stable prefix that providers may cache.
    /// </summary>
    public sealed class ConversationContextApplicationService
    {
        private readonly ConversationSummaryApplicationService _summaryService = new();

        /// <summary>
        /// Trims one provider-facing conversation while preserving an early anchor and the most recent turns.
        /// Older retained user/assistant turns are marked as cache-preferred when appropriate.
        /// </summary>
        public List<ChatMessage> BuildContextWindow(
            IReadOnlyList<ChatMessage> history,
            int maxMessages = 40,
            int anchorMessages = 6,
            int uncachedRecentMessages = 12,
            bool enablePromptCaching = true,
            bool enableConversationSummaries = true,
            int summaryMaxCharacters = 900)
        {
            var system = history.Where(m => string.Equals(m.Role, "system", StringComparison.OrdinalIgnoreCase))
                .Select(Clone)
                .ToList();

            var indexedNonSystem = history
                .Select((message, index) => (message, index))
                .Where(pair => !string.Equals(pair.message.Role, "system", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (indexedNonSystem.Count == 0)
                return system;

            List<(ChatMessage message, int index)> retained;
            List<(ChatMessage message, int index)> omitted = new();
            if (indexedNonSystem.Count <= maxMessages)
            {
                retained = indexedNonSystem;
            }
            else
            {
                var anchors = indexedNonSystem.Take(Math.Min(anchorMessages, maxMessages));
                var tailCount = Math.Max(0, maxMessages - anchors.Count());
                var tail = indexedNonSystem.Skip(Math.Max(0, indexedNonSystem.Count - tailCount));
                retained = anchors
                    .Concat(tail)
                    .GroupBy(pair => pair.index)
                    .Select(group => group.First())
                    .OrderBy(pair => pair.index)
                    .ToList();

                var retainedIndices = retained.Select(pair => pair.index).ToHashSet();
                omitted = indexedNonSystem.Where(pair => !retainedIndices.Contains(pair.index)).ToList();
            }

            var cacheBoundary = Math.Max(0, retained.Count - uncachedRecentMessages);
            var result = new List<ChatMessage>(system.Count + retained.Count + 1);
            result.AddRange(system);

            if (enableConversationSummaries && omitted.Count > 0)
            {
                var summary = _summaryService.BuildSummaryMessage(
                    omitted.Select(pair => pair.message).ToList(),
                    summaryMaxCharacters);
                if (summary != null)
                    result.Add(summary);
            }

            for (var i = 0; i < retained.Count; i++)
            {
                var copy = Clone(retained[i].message);
                copy.PreferPromptCache =
                    enablePromptCaching &&
                    i < cacheBoundary &&
                    (string.Equals(copy.Role, "user", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(copy.Role, "assistant", StringComparison.OrdinalIgnoreCase));

                if (!string.IsNullOrWhiteSpace(copy.ImagePath) || copy.ImageBytes?.Length > 0)
                    copy.PreferPromptCache = false;

                result.Add(copy);
            }

            return result;
        }

        private static ChatMessage Clone(ChatMessage message)
            => new()
            {
                Role = message.Role,
                Content = message.Content,
                ImagePath = message.ImagePath,
                ImageBytes = message.ImageBytes,
                ImageMimeType = message.ImageMimeType,
                PreferPromptCache = message.PreferPromptCache
            };
    }
}
