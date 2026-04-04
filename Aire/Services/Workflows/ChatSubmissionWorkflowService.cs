using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Aire.Data;
using Aire.Providers;
using Aire.Services;

namespace Aire.Services.Workflows
{
    /// <summary>
    /// Prepares a user-submitted chat message for persistence, UI display, and provider history.
    /// </summary>
    public sealed class ChatSubmissionWorkflowService
    {
        public sealed record PreparedSubmission(
            string PersistedContent,
            string DisplayContent,
            string? HistoryImagePath,
            IReadOnlyList<MessageAttachment> Attachments,
            string? SuggestedConversationTitle);

        /// <summary>
        /// Normalizes one user submission, optionally inlining text-file attachments and generating a title suggestion.
        /// </summary>
        /// <param name="userText">Raw text from the input box.</param>
        /// <param name="attachedImagePath">Optional attached image path.</param>
        /// <param name="attachedFilePath">Optional attached file path.</param>
        /// <param name="textExtensions">Extensions that should be treated as inline text attachments.</param>
        /// <param name="conversationHistoryCount">Current number of provider-history entries before the message is added.</param>
        /// <returns>The normalized message payload for persistence, UI display, and provider history.</returns>
        public PreparedSubmission PrepareSubmission(
            string userText,
            string? attachedImagePath,
            string? attachedFilePath,
            ISet<string> textExtensions,
            int conversationHistoryCount)
        {
            var messageContent = userText;
            var attachments = new List<MessageAttachment>();
            if (!string.IsNullOrEmpty(attachedFilePath) && textExtensions.Contains(Path.GetExtension(attachedFilePath)))
            {
                try
                {
                    var fileText = File.ReadAllText(attachedFilePath);
                    if (fileText.Length > 100_000)
                        fileText = fileText[..100_000] + "\n... [truncated at 100 KB]";
                    var lang = Path.GetExtension(attachedFilePath).TrimStart('.').ToLowerInvariant();
                    var fileName = Path.GetFileName(attachedFilePath);
                    messageContent = userText + $"\n\n**Attached: {fileName}**\n```{lang}\n{fileText}\n```";
                    attachments.Add(BuildAttachment(attachedFilePath, isImage: false, isInlinePreview: true));
                }
                catch (Exception ex)
                {
                    AppLogger.Error("ChatSubmission.BuildContent", $"Failed to read attached file '{attachedFilePath}'", ex);
                }
            }

            string? historyImagePath = attachedImagePath;
            if (!string.IsNullOrEmpty(attachedImagePath))
                attachments.Add(BuildAttachment(attachedImagePath, isImage: true, isInlinePreview: true));

            if (historyImagePath == null &&
                !string.IsNullOrEmpty(attachedFilePath) &&
                IsImageFile(attachedFilePath))
            {
                historyImagePath = attachedFilePath;
                attachments.Add(BuildAttachment(attachedFilePath, isImage: true, isInlinePreview: true));
            }

            if (historyImagePath == null &&
                !string.IsNullOrEmpty(attachedFilePath) &&
                !textExtensions.Contains(Path.GetExtension(attachedFilePath)))
            {
                attachments.Add(BuildAttachment(attachedFilePath, isImage: false, isInlinePreview: false));
                var fileInfo = new FileInfo(attachedFilePath);
                var fileSize = File.Exists(attachedFilePath) ? fileInfo.Length : 0;
                messageContent = string.IsNullOrWhiteSpace(messageContent)
                    ? $"Attached file: {fileInfo.Name} ({FormatFileSize(fileSize)})"
                    : $"{messageContent}\n\nAttached file: {fileInfo.Name} ({FormatFileSize(fileSize)})";
            }

            string? title = null;
            if (conversationHistoryCount == 0)
            {
                title = userText.Replace('\n', ' ').Trim();
                if (title.Length > 60)
                    title = title[..60] + "…";
            }

            return new PreparedSubmission(
                messageContent,
                messageContent,
                historyImagePath,
                attachments,
                title);
        }

        /// <summary>
        /// Appends a unique input to the input-history list and resets transient cursor state.
        /// </summary>
        /// <param name="history">Input-history collection to mutate.</param>
        /// <param name="text">Current submitted text.</param>
        /// <returns>The reset history index and cleared draft text.</returns>
        public (int HistoryIndex, string Draft) UpdateInputHistory(IList<string> history, string text)
        {
            if (history.Count == 0 || !string.Equals(history[^1], text, StringComparison.Ordinal))
                history.Add(text);

            return (-1, string.Empty);
        }

        /// <summary>
        /// Builds the provider-facing history message for the just-submitted user turn.
        /// </summary>
        /// <param name="content">Normalized user message content.</param>
        /// <param name="historyImagePath">Optional image or binary attachment path.</param>
        /// <param name="attachments">Optional attachment metadata for provider history.</param>
        /// <returns>The provider-facing chat message to append to history.</returns>
        public ChatMessage BuildProviderHistoryMessage(
            string content,
            string? historyImagePath,
            IReadOnlyList<MessageAttachment>? attachments)
            => new()
            {
                Role = "user",
                Content = content,
                ImagePath = historyImagePath,
                Attachments = attachments?.ToList()
            };

        private static MessageAttachment BuildAttachment(string filePath, bool isImage, bool isInlinePreview)
        {
            var info = new FileInfo(filePath);
            return new MessageAttachment
            {
                FilePath = filePath,
                FileName = info.Name,
                MimeType = GuessMimeType(filePath),
                SizeBytes = File.Exists(filePath) ? info.Length : 0,
                IsImage = isImage,
                IsInlinePreview = isInlinePreview
            };
        }

        private static string? GuessMimeType(string filePath)
            => Path.GetExtension(filePath).ToLowerInvariant() switch
            {
                ".png" => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".bmp" => "image/bmp",
                ".gif" => "image/gif",
                ".webp" => "image/webp",
                ".svg" => "image/svg+xml",
                ".pdf" => "application/pdf",
                ".zip" => "application/zip",
                ".json" => "application/json",
                ".txt" => "text/plain",
                ".md" => "text/markdown",
                _ => "application/octet-stream"
            };

        private static bool IsImageFile(string filePath)
            => Path.GetExtension(filePath).ToLowerInvariant() is ".png" or ".jpg" or ".jpeg" or ".bmp" or ".gif" or ".webp" or ".svg" or ".tiff";

        private static string FormatFileSize(long bytes) => bytes switch
        {
            < 1024 => $"{bytes} B",
            < 1_048_576 => $"{bytes / 1024.0:F1} KB",
            _ => $"{bytes / 1_048_576.0:F1} MB"
        };
    }
}
