using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Aire.Providers;
using Aire.Services;
using Aire.Data;

namespace Aire.Services
{
    /// <summary>
    /// Gradually compacts older conversation turns by merging adjacent messages,
    /// preserving essential structure (tool calls, code blocks) while reducing token count.
    /// </summary>
    public sealed class ConversationCompactionService
    {
        /// <summary>
        /// Merges adjacent user/assistant pairs (or user/tool/assistant sequences) into condensed messages
        /// until the total token count is below <paramref name="targetTokenCount"/> or no more merges are possible.
        /// </summary>
        /// <param name="messages">Conversation history in chronological order (oldest first).</param>
        /// <param name="targetTokenCount">Desired maximum total token count after compaction.</param>
        /// <param name="estimator">Token estimator used to measure message sizes.</param>
        /// <param name="modelId">Optional model identifier for token estimation.</param>
        /// <returns>
        /// A compacted list of messages where older turns have been merged.
        /// The returned messages are new instances; the original list is not mutated.
        /// </returns>
        public IEnumerable<ChatMessage> CompactConversation(
            IEnumerable<ChatMessage> messages,
            int targetTokenCount,
            ITokenEstimator estimator,
            string? modelId = null)
        {
            var messageList = messages.ToList();
            if (messageList.Count == 0)
                return messageList;

            // Compute token counts for each message
            var tokenCounts = messageList.Select(m => EstimateMessageTokens(m, estimator, modelId)).ToList();
            int totalTokens = tokenCounts.Sum();

            // If already under target, return a copy
            if (totalTokens <= targetTokenCount)
                return messageList.Select(Clone).ToList();

            // Working copies
            var workingMessages = messageList.Select(Clone).ToList();
            var workingTokens = new List<int>(tokenCounts);

            // Iterate from oldest to newest, merging adjacent pairs
            for (int i = 0; i < workingMessages.Count - 1 && totalTokens > targetTokenCount; i++)
            {
                // Skip if either message already contains a tool call (preserve them)
                if (ContainsToolCall(workingMessages[i]) || ContainsToolCall(workingMessages[i + 1]))
                    continue;

                // Only merge user/assistant or user/tool/assistant sequences
                if (!IsMergeablePair(workingMessages[i], workingMessages[i + 1]))
                    continue;

                // Merge the two messages into one
                var merged = MergeMessages(workingMessages[i], workingMessages[i + 1]);
                int mergedTokens = EstimateMessageTokens(merged, estimator, modelId);

                // If merging doesn't reduce tokens enough, skip
                if (mergedTokens >= workingTokens[i] + workingTokens[i + 1])
                    continue;

                // Replace the two messages with the merged one
                workingMessages[i] = merged;
                workingMessages.RemoveAt(i + 1);
                workingTokens[i] = mergedTokens;
                workingTokens.RemoveAt(i + 1);
                totalTokens = workingTokens.Sum();

                // Stay at same index to evaluate the new pair (i now points to merged message)
                i--;
            }

            return workingMessages;
        }

        private static bool ContainsToolCall(ChatMessage message)
        {
            return message.Content?.Contains("<tool_call>") == true;
        }

        private static bool IsMergeablePair(ChatMessage first, ChatMessage second)
        {
            // Allow user→assistant, user→tool, tool→assistant, assistant→user? (usually not)
            // For simplicity, allow any adjacent pair where at least one is user or assistant
            var role1 = first.Role.ToLowerInvariant();
            var role2 = second.Role.ToLowerInvariant();
            return (role1 == "user" && role2 == "assistant") ||
                   (role1 == "user" && role2 == "tool") ||
                   (role1 == "tool" && role2 == "assistant") ||
                   (role1 == "assistant" && role2 == "user");
        }

        private static ChatMessage MergeMessages(ChatMessage a, ChatMessage b)
        {
            // Determine merged role: prefer "assistant" if present, else "user"
            string mergedRole = a.Role.ToLowerInvariant() == "assistant" || b.Role.ToLowerInvariant() == "assistant"
                ? "assistant"
                : a.Role.ToLowerInvariant() == "tool" ? "tool" : "user";

            // Combine content with a simple separator
            string mergedContent = CondenseContent(a.Content ?? string.Empty, b.Content ?? string.Empty);

            return new ChatMessage
            {
                Role = mergedRole,
                Content = mergedContent,
                ImagePath = a.ImagePath ?? b.ImagePath,
                ImageBytes = a.ImageBytes ?? b.ImageBytes,
                ImageMimeType = a.ImageMimeType ?? b.ImageMimeType,
                Attachments = a.Attachments ?? b.Attachments,
                PreferPromptCache = true // mark as cache-friendly
            };
        }

        private static string CondenseContent(string contentA, string contentB)
        {
            var linesA = contentA.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                                 .Select(line => line.Trim())
                                 .Where(line => line.Length > 0);
            var linesB = contentB.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                                 .Select(line => line.Trim())
                                 .Where(line => line.Length > 0);

            // Take up to 2 structured fact lines from each, then first sentence
            var structured = linesA.Concat(linesB)
                                   .Where(IsStructuredFactLine)
                                   .Take(3)
                                   .ToList();
            if (structured.Count > 0)
                return string.Join(" | ", structured);

            // Otherwise, take first sentence from each, joined by "; "
            var firstSentenceA = linesA.FirstOrDefault() ?? "";
            var firstSentenceB = linesB.FirstOrDefault() ?? "";
            if (string.IsNullOrEmpty(firstSentenceB))
                return firstSentenceA.Length <= 120 ? firstSentenceA : firstSentenceA[..117] + "...";
            if (string.IsNullOrEmpty(firstSentenceA))
                return firstSentenceB.Length <= 120 ? firstSentenceB : firstSentenceB[..117] + "...";

            var combined = $"{firstSentenceA}; {firstSentenceB}";
            return combined.Length <= 120 ? combined : combined[..117] + "...";
        }

        private static bool IsStructuredFactLine(string line)
        {
            if (line.StartsWith("- ", StringComparison.Ordinal) ||
                line.StartsWith("* ", StringComparison.Ordinal))
                return true;

            var colonIndex = line.IndexOf(':');
            return colonIndex > 0 && colonIndex < 32;
        }

        private static ImageMetadata CreateImageMetadataFromMessage(ChatMessage message)
        {
            // Determine width/height (unknown)
            int width = 0;
            int height = 0;
            string format = "unknown";
            long sizeBytes = 0;
            string? detailLevel = null;

            // If we have ImagePath, try to extract file extension for format
            if (!string.IsNullOrEmpty(message.ImagePath))
            {
                var ext = System.IO.Path.GetExtension(message.ImagePath)?.TrimStart('.');
                if (!string.IsNullOrEmpty(ext))
                    format = ext.ToLowerInvariant();
                // Could attempt to read file size but skip for performance
                try
                {
                    var fileInfo = new System.IO.FileInfo(message.ImagePath);
                    if (fileInfo.Exists)
                        sizeBytes = fileInfo.Length;
                }
                catch { }
            }
            else if (message.ImageBytes?.Length > 0)
            {
                sizeBytes = message.ImageBytes.Length;
                // Could guess format from MIME type if present
                if (!string.IsNullOrEmpty(message.ImageMimeType))
                {
                    // map common mime types to format
                    if (message.ImageMimeType.Contains("jpeg") || message.ImageMimeType.Contains("jpg"))
                        format = "jpeg";
                    else if (message.ImageMimeType.Contains("png"))
                        format = "png";
                    else if (message.ImageMimeType.Contains("gif"))
                        format = "gif";
                    else if (message.ImageMimeType.Contains("webp"))
                        format = "webp";
                }
            }

            return new ImageMetadata
            {
                Width = width,
                Height = height,
                Format = format,
                SizeBytes = sizeBytes,
                FilePath = message.ImagePath,
                DetailLevel = detailLevel
            };
        }

        private static int EstimateMessageTokens(ChatMessage message, ITokenEstimator estimator, string? modelId)
        {
            int tokens = 0;
            if (!string.IsNullOrEmpty(message.Content))
                tokens += estimator.EstimateTokens(message.Content, modelId);
            if (!string.IsNullOrEmpty(message.ImagePath) || message.ImageBytes?.Length > 0)
            {
                var imageMetadata = CreateImageMetadataFromMessage(message);
                try
                {
                    tokens += estimator.EstimateTokensForImage(imageMetadata);
                }
                catch (NotImplementedException)
                {
                    // fallback
                    bool isHighDetail = imageMetadata.DetailLevel == "high" || (imageMetadata.Width > 0 && imageMetadata.Height > 0);
                    tokens += isHighDetail ? 170 : 85;
                    AppLogger.Warn(nameof(ConversationCompactionService), "ITokenEstimator.EstimateTokensForImage not implemented, using default image token count.");
                }
            }
            if (message.Attachments?.Count > 0)
                tokens += estimator.EstimateTokensForAttachments(message.Attachments);
            return tokens;
        }

        private static ChatMessage Clone(ChatMessage message)
        {
            return new ChatMessage
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
}