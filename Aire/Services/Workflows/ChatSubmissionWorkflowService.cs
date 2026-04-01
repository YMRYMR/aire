using System;
using System.Collections.Generic;
using System.IO;
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
                }
                catch (Exception ex)
                {
                    AppLogger.Error("ChatSubmission.BuildContent", $"Failed to read attached file '{attachedFilePath}'", ex);
                }
            }

            string? historyImagePath = attachedImagePath;
            if (historyImagePath == null &&
                !string.IsNullOrEmpty(attachedFilePath) &&
                !textExtensions.Contains(Path.GetExtension(attachedFilePath)))
            {
                historyImagePath = attachedFilePath;
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
        /// <returns>The provider-facing chat message to append to history.</returns>
        public ChatMessage BuildProviderHistoryMessage(string content, string? historyImagePath)
            => new()
            {
                Role = "user",
                Content = content,
                ImagePath = historyImagePath
            };
    }
}
