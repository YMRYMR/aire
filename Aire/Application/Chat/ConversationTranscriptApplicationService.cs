using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Aire.Data;
using ProviderChatMessage = Aire.Providers.ChatMessage;

namespace Aire.AppLayer.Chat
{
    /// <summary>
    /// Application-layer shaping for turning persisted conversation messages into a view-neutral transcript plan.
    /// </summary>
    public sealed class ConversationTranscriptApplicationService
    {
        private readonly AssistantImageResponseApplicationService _assistantImageResponse = new();

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
            int MessageId,
            TranscriptRole Role,
            string Sender,
            string Text,
            DateTime CreatedAt,
            IReadOnlyList<string> ImageReferences,
            IReadOnlyList<MessageAttachment> FileAttachments,
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
                    var parsedAssistant = _assistantImageResponse.Parse(msg.Content);
                    history.Add(new ProviderChatMessage { Role = "assistant", Content = parsedAssistant.Text });
                }

                bool startsNewDateSection =
                    sender != "System" &&
                    (lastDate == null || lastDate.Value.Date != msg.CreatedAt.Date);
                if (startsNewDateSection)
                    lastDate = msg.CreatedAt;

                var entryText = msg.Content;
                var attachments = msg.Attachments?.Count > 0
                    ? msg.Attachments
                    : DeserializeAttachments(msg.AttachmentsJson);

                IReadOnlyList<string> imageReferences = attachments
                    .Where(attachment => attachment.IsImage && !string.IsNullOrWhiteSpace(attachment.FilePath))
                    .Select(attachment => attachment.FilePath)
                    .ToList();
                if (imageReferences.Count == 0 &&
                    attachments.Count == 0 &&
                    IsLegacyImagePath(msg.ImagePath))
                    imageReferences = new[] { msg.ImagePath! };

                var fileAttachments = attachments
                    .Where(attachment => !attachment.IsImage)
                    .ToList();

                if (role == TranscriptRole.Assistant)
                {
                    var parsedAssistant = _assistantImageResponse.Parse(msg.Content);
                    entryText = parsedAssistant.Text;
                    if (parsedAssistant.ImageReferences.Count > 0)
                        imageReferences = parsedAssistant.ImageReferences;
                }

                entries.Add(new TranscriptEntry(
                    msg.Id,
                    role,
                    sender,
                    entryText,
                    msg.CreatedAt,
                    imageReferences,
                    fileAttachments,
                    startsNewDateSection));
            }

            return new ConversationTranscriptPlan(entries, history, inputHistory);
        }

        private static List<MessageAttachment> DeserializeAttachments(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return new List<MessageAttachment>();

            try
            {
                return JsonSerializer.Deserialize<List<MessageAttachment>>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }) ?? new List<MessageAttachment>();
            }
            catch
            {
                return new List<MessageAttachment>();
            }
        }

        private static bool IsLegacyImagePath(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return false;

            var extension = Path.GetExtension(path);
            return extension.Equals(".png", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".gif", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".bmp", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".webp", StringComparison.OrdinalIgnoreCase);
        }
    }
}
