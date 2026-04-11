using System;
using System.Collections.Generic;
using System.Linq;
using Aire.Providers;
using Aire.Services;
using Aire.Data;

namespace Aire.AppLayer.Chat
{
    /// <summary>
    /// Builds a bounded conversation window and marks the stable prefix that providers may cache.
    /// </summary>
    public sealed class ConversationContextApplicationService
    {
        private readonly ConversationSummaryApplicationService _summaryService = new();
        private readonly ITokenEstimator _tokenEstimator;
        private readonly IContextTriggerDetector _triggerDetector;

        /// <summary>
        /// Initializes a new instance of the <see cref="ConversationContextApplicationService"/> class
        /// with default token estimator and trigger detector.
        /// </summary>
        public ConversationContextApplicationService()
            : this(new TokenEstimatorRegistry().GetEstimator(""), new KeywordContextTriggerDetector())
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ConversationContextApplicationService"/> class
        /// with custom token estimator and trigger detector (used for testing).
        /// </summary>
        public ConversationContextApplicationService(ITokenEstimator tokenEstimator, IContextTriggerDetector triggerDetector)
        {
            _tokenEstimator = tokenEstimator ?? throw new ArgumentNullException(nameof(tokenEstimator));
            _triggerDetector = triggerDetector ?? throw new ArgumentNullException(nameof(triggerDetector));
        }

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
            int summaryMaxCharacters = 900,
            int? maxTokens = null,
            int anchorTokens = 0,
            int tailTokens = 0,
            bool enableTokenAwareTruncation = false,
            bool enableToolFocusWindow = false,
            bool enableRetryFollowUpWindow = false,
            IReadOnlyDictionary<string, int>? perMessageTypeLimits = null,
            string? providerType = null,
            string? modelId = null,
            bool enableGradualCompaction = false,
            int compactionTokenThreshold = 0)
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

                if (copy.Attachments?.Count > 0)
                    copy.PreferPromptCache = false;

                result.Add(copy);
            }

            // Apply gradual compaction if enabled and token count exceeds threshold
            if (enableGradualCompaction && compactionTokenThreshold > 0)
            {
                var totalTokens = result.Sum(m => EstimateMessageTokens(m, modelId));
                if (totalTokens > compactionTokenThreshold)
                {
                    // Determine anchor and tail message counts
                    int tailCount = maxMessages - anchorMessages;
                    // Identify which messages in result are from retained (excluding system and summary)
                    int systemCount = system.Count;
                    bool hasSummary = result.Count > systemCount && result[systemCount].Role == "summary";
                    int summaryOffset = hasSummary ? 1 : 0;
                    int retainedStart = systemCount + summaryOffset;
                    int retainedCount = result.Count - retainedStart;
                    if (retainedCount > 0)
                    {
                        // Determine anchor and tail indices within retained list
                        int anchorCount = Math.Min(anchorMessages, retainedCount);
                        int actualTailCount = Math.Min(tailCount, retainedCount - anchorCount);
                        int middleStart = anchorCount;
                        int middleCount = retainedCount - anchorCount - actualTailCount;
                        if (middleCount > 0)
                        {
                            var middleMessages = result.Skip(retainedStart + middleStart).Take(middleCount).ToList();
                            var compactionService = new ConversationCompactionService();
                            var compacted = compactionService.CompactConversation(
                                middleMessages,
                                compactionTokenThreshold - (totalTokens - middleMessages.Sum(m => EstimateMessageTokens(m, modelId))),
                                _tokenEstimator,
                                modelId);
                            // Replace middle messages with compacted ones
                            result.RemoveRange(retainedStart + middleStart, middleCount);
                            result.InsertRange(retainedStart + middleStart, compacted);
                        }
                    }
                }
            }

            return result;
        }

        private static Aire.Data.Message ToDataMessage(ChatMessage chatMessage)
        {
            return new Aire.Data.Message
            {
                Role = chatMessage.Role,
                Content = chatMessage.Content,
                ImagePath = chatMessage.ImagePath,
                // AttachmentsJson not needed for trigger detection
                Attachments = chatMessage.Attachments?.Select(a => new Aire.Data.MessageAttachment
                {
                    // Simplified mapping; actual properties may differ
                    FileName = a.FileName,
                    MimeType = a.MimeType,
                    SizeBytes = a.SizeBytes
                }).ToList() ?? new List<Aire.Data.MessageAttachment>()
            };
        }

        private int EstimateMessageTokens(ChatMessage message, string? modelId = null)
        {
            int tokens = 0;
            if (!string.IsNullOrEmpty(message.Content))
                tokens += _tokenEstimator.EstimateTokens(message.Content, modelId);
            if (!string.IsNullOrEmpty(message.ImagePath) || message.ImageBytes?.Length > 0)
            {
                // Estimate image tokens (simplified: assume a fixed cost)
                // In real implementation we would parse image metadata.
                tokens += 100; // placeholder
            }
            if (message.Attachments?.Count > 0)
                tokens += _tokenEstimator.EstimateTokensForAttachments(message.Attachments);
            return tokens;
        }

        private (List<(ChatMessage message, int index)> retained, List<(ChatMessage message, int index)> omitted)
            SelectWindowTokenAware(
                List<(ChatMessage message, int index)> indexedNonSystem,
                int? maxTokens,
                int anchorTokens,
                int tailTokens,
                bool enableToolFocusWindow,
                bool enableRetryFollowUpWindow,
                IReadOnlyDictionary<string, int>? perMessageTypeLimits,
                string? providerType,
                string? modelId)
        {
            // If token-aware truncation is not enabled or maxTokens not set, fall back to message-count algorithm (caller should handle)
            if (!maxTokens.HasValue)
                return (indexedNonSystem, new List<(ChatMessage message, int index)>());

            // Compute token counts for each message
            var messageTokens = indexedNonSystem.Select(pair =>
            {
                var tokens = EstimateMessageTokens(pair.message, modelId);
                return (pair.message, pair.index, tokens);
            }).ToList();

            // Detect triggers if needed
            var recentMessages = indexedNonSystem.TakeLast(5).Select(pair => ToDataMessage(pair.message)).ToList();
            var detection = _triggerDetector.DetectTriggers(recentMessages);

            // Apply special windows based on triggers
            if (enableToolFocusWindow && detection.IsToolFocus)
            {
                // Tight window: latest user message + 2 preceding messages, plus previous user turn for retry follow-ups
                // Simplified: take last 3 messages
                var lastThree = indexedNonSystem.TakeLast(3).ToList();
                return (lastThree, indexedNonSystem.Except(lastThree).ToList());
            }

            if (enableRetryFollowUpWindow && detection.IsRetryFollowUp)
            {
                // Expand window to include previous user turn and assistant response
                // Simplified: take last 4 messages
                var lastFour = indexedNonSystem.TakeLast(4).ToList();
                return (lastFour, indexedNonSystem.Except(lastFour).ToList());
            }

            // Token-based sliding window with anchor and tail tokens
            int totalTokens = 0;
            var retained = new List<(ChatMessage message, int index)>();
            var omitted = new List<(ChatMessage message, int index)>();

            // Anchor tokens from start
            int anchorTokenBudget = anchorTokens;
            for (int i = 0; i < indexedNonSystem.Count && anchorTokenBudget > 0; i++)
            {
                var token = messageTokens[i].tokens;
                if (anchorTokenBudget >= token)
                {
                    retained.Add((indexedNonSystem[i].message, indexedNonSystem[i].index));
                    totalTokens += token;
                    anchorTokenBudget -= token;
                }
                else
                {
                    // Not enough budget for this message, skip it (omit)
                    omitted.Add((indexedNonSystem[i].message, indexedNonSystem[i].index));
                }
            }

            // Tail tokens from end
            int tailTokenBudget = tailTokens;
            for (int i = indexedNonSystem.Count - 1; i >= 0 && tailTokenBudget > 0; i--)
            {
                if (retained.Any(r => r.index == indexedNonSystem[i].index))
                    continue; // already retained
                var token = messageTokens[i].tokens;
                if (tailTokenBudget >= token)
                {
                    retained.Insert(0, (indexedNonSystem[i].message, indexedNonSystem[i].index)); // keep order later
                    totalTokens += token;
                    tailTokenBudget -= token;
                }
                else
                {
                    omitted.Add((indexedNonSystem[i].message, indexedNonSystem[i].index));
                }
            }

            // Ensure total tokens does not exceed maxTokens (if exceeded, drop from tail)
            while (totalTokens > maxTokens.Value && retained.Count > 0)
            {
                var last = retained[retained.Count - 1];
                var token = messageTokens.First(m => m.index == last.index).tokens;
                retained.RemoveAt(retained.Count - 1);
                totalTokens -= token;
                omitted.Add(last);
            }

            // Sort retained by original index
            retained = retained.OrderBy(r => r.index).ToList();
            omitted = omitted.OrderBy(o => o.index).ToList();

            return (retained, omitted);
        }

        private static ChatMessage Clone(ChatMessage message)
            => new()
            {
                Role = message.Role,
                Content = message.Content,
                ImagePath = message.ImagePath,
                ImageBytes = message.ImageBytes,
                ImageMimeType = message.ImageMimeType,
                Attachments = message.Attachments?.ToList(),
                PreferPromptCache = message.PreferPromptCache
            };
    }
}
