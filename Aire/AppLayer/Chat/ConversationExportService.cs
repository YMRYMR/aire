using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Aire.AppLayer.Abstractions;
using Aire.Data;

namespace Aire.AppLayer.Chat
{
    /// <summary>
    /// Exports a conversation to Markdown format.
    /// </summary>
    public sealed class ConversationExportService
    {
        private readonly IConversationRepository _conversations;
        private readonly IProviderRepository _providers;

        public ConversationExportService(IConversationRepository conversations, IProviderRepository providers)
        {
            _conversations = conversations;
            _providers = providers;
        }

        /// <summary>
        /// Exports a conversation as Markdown text.
        /// </summary>
        public async Task<string> ExportMarkdownAsync(int conversationId)
        {
            var conversation = await _conversations.GetConversationAsync(conversationId);
            if (conversation == null)
                throw new ArgumentException($"Conversation {conversationId} not found.");

            var messages = await _conversations.GetMessagesAsync(conversationId);
            var providers = await _providers.GetProvidersAsync();
            var provider = providers.Find(p => p.Id == conversation.ProviderId);

            var sb = new StringBuilder();
            sb.AppendLine($"# {conversation.Title}");
            sb.AppendLine();

            if (provider != null)
            {
                sb.AppendLine($"**Provider:** {provider.Name} ({provider.Model})");
                sb.AppendLine($"**Date:** {conversation.CreatedAt:yyyy-MM-dd HH:mm}");
                sb.AppendLine();
            }

            sb.AppendLine("---");
            sb.AppendLine();

            foreach (var msg in messages)
            {
                var sender = msg.Role switch
                {
                    "user" => "User",
                    "assistant" => "AI",
                    "system" => "System",
                    _ => msg.Role
                };

                var timestamp = msg.CreatedAt != default ? $" ({msg.CreatedAt:HH:mm})" : "";

                sb.AppendLine($"### {sender}{timestamp}");
                sb.AppendLine();

                if (!string.IsNullOrWhiteSpace(msg.Content))
                {
                    // Clean up tool-call artifacts for readability.
                    var content = msg.Content;
                    if (content.Contains("[switch_model result]"))
                        content = CleanToolResult(content);

                    sb.AppendLine(content);
                    sb.AppendLine();
                }

                if (!string.IsNullOrWhiteSpace(msg.ImagePath))
                {
                    sb.AppendLine($"*[Image attached: {Path.GetFileName(msg.ImagePath)}]*");
                    sb.AppendLine();
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Exports a conversation and saves it to a file.
        /// </summary>
        public async Task<string> ExportToFileAsync(int conversationId, string filePath)
        {
            var markdown = await ExportMarkdownAsync(conversationId);
            await File.WriteAllTextAsync(filePath, markdown, Encoding.UTF8);
            return filePath;
        }

        private static string CleanToolResult(string content)
        {
            // Remove verbose tool-result prefixes for cleaner export.
            return content
                .Replace("[switch_model result]: SUCCESS — ", "Switched model: ")
                .Replace("[switch_model result]: ERROR — ", "Model switch error: ");
        }
    }
}
