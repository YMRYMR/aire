using System;
using System.Collections.Generic;
using Aire.Data;
using ProviderChatMessage = Aire.Providers.ChatMessage;

namespace Aire.AppLayer.Chat
{
    /// <summary>
    /// Application-layer shaping for turning persisted conversation messages into a view-neutral transcript plan.
    /// </summary>
    public sealed class ConversationTranscriptApplicationService
    {
        /// <summary>
        /// Logical transcript role used by the UI to choose styling and rendering behavior.
        /// </summary>
        public enum TranscriptRole
        {
            User,
            Assistant,
            Tool,
            System
        }

        /// <summary>
        /// One transcript entry ready for UI rendering.
        /// </summary>
        public sealed record TranscriptEntry(
            TranscriptRole Role,
            string Sender,
            string Text,
            DateTime CreatedAt,
            string? ImagePath,
            bool StartsNewDateSection);

        /// <summary>
        /// View-neutral result of reconstructing one stored conversation transcript.
        /// </summary>
        public sealed record ConversationTranscriptPlan(
            IReadOnlyList<TranscriptEntry> Entries,
            IReadOnlyList<ProviderChatMessage> ConversationHistory,
            IReadOnlyList<string> InputHistory);

        /// <summary>
        /// Builds a transcript plan from persisted conversation messages.
        /// </summary>
        /// <param name="messages">Stored conversation messages ordered by creation time.</param>
        /// <returns>The transcript entries plus provider-history and input-history data needed by the UI.</returns>
        public ConversationTranscriptPlan BuildTranscript(IReadOnlyList<Aire.Data.Message> messages)
        {
            var entries = new List<TranscriptEntry>(messages.Count);
            var history = new List<ProviderChatMessage>();
            var inputHistory = new List<string>();
            DateTime? lastDate = null;

            foreach (var msg in messages)
            {
                var role = msg.Role.ToLowerInvariant() switch
                {
                    "user" => TranscriptRole.User,
                    "assistant" => TranscriptRole.Assistant,
                    "tool" => TranscriptRole.Tool,
                    _ => TranscriptRole.System
                };

                var sender = role switch
                {
                    TranscriptRole.User => "You",
                    TranscriptRole.Assistant => "AI",
                    TranscriptRole.Tool => "AI",
                    _ => "System"
                };

                if (role == TranscriptRole.User)
                {
                    history.Add(new ProviderChatMessage { Role = "user", Content = msg.Content });
                    if (inputHistory.Count == 0 || inputHistory[^1] != msg.Content)
                        inputHistory.Add(msg.Content);
                }
                else if (role == TranscriptRole.Assistant)
                {
                    history.Add(new ProviderChatMessage { Role = "assistant", Content = msg.Content });
                }

                bool startsNewDateSection =
                    sender != "System" &&
                    (lastDate == null || lastDate.Value.Date != msg.CreatedAt.Date);
                if (startsNewDateSection)
                    lastDate = msg.CreatedAt;

                entries.Add(new TranscriptEntry(
                    role,
                    sender,
                    msg.Content,
                    msg.CreatedAt,
                    msg.ImagePath,
                    startsNewDateSection));
            }

            return new ConversationTranscriptPlan(entries, history, inputHistory);
        }
    }
}
