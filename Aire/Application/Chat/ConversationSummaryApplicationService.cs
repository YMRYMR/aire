using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Aire.Providers;

namespace Aire.AppLayer.Chat
{
    /// <summary>
    /// Builds a compact synthetic summary for older conversation turns that were trimmed
    /// out of the active provider context.
    /// </summary>
    public sealed class ConversationSummaryApplicationService
    {
        private const string SummaryHeader = "Conversation summary of earlier omitted context:";

        public ChatMessage? BuildSummaryMessage(
            IReadOnlyList<ChatMessage> omittedMessages,
            int maxCharacters = 900)
        {
            if (omittedMessages.Count == 0 || maxCharacters < 120)
                return null;

            var sb = new StringBuilder();
            sb.AppendLine(SummaryHeader);

            foreach (var group in omittedMessages
                .Where(m => !string.Equals(m.Role, "system", StringComparison.OrdinalIgnoreCase))
                .GroupBy(GetRoleLabel))
            {
                var items = group
                    .Select(BuildSnippet)
                    .Where(text => !string.IsNullOrWhiteSpace(text))
                    .Distinct(StringComparer.Ordinal)
                    .Take(6)
                    .ToList();

                if (items.Count == 0)
                    continue;

                sb.Append(group.Key);
                sb.Append(": ");
                sb.AppendLine(string.Join(" | ", items));
            }

            var text = sb.ToString().Trim();
            if (text.Length <= SummaryHeader.Length)
                return null;

            if (text.Length > maxCharacters)
                text = text[..Math.Max(0, maxCharacters - 1)].TrimEnd() + "…";

            return new ChatMessage
            {
                Role = "system",
                Content = text,
                PreferPromptCache = false
            };
        }

        private static string Sanitize(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            var collapsed = string.Join(" ",
                text.Split(new[] { '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(part => part.Trim())
                    .Where(part => part.Length > 0));

            return collapsed.Length <= 120
                ? collapsed
                : collapsed[..117].TrimEnd() + "...";
        }

        private static string GetRoleLabel(ChatMessage message)
        {
            if (TryGetSemanticLabel(message.Content, out var semanticLabel))
                return semanticLabel;

            return message.Role.ToLowerInvariant() switch
            {
                "assistant" => "Assistant",
                "tool" => "Tool",
                _ => "User"
            };
        }

        private static string BuildSnippet(ChatMessage message)
        {
            var meaningfulLines = (message.Content ?? string.Empty)
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Trim())
                .Where(line => line.Length > 0)
                .ToList();

            if (meaningfulLines.Count == 0)
                return string.Empty;

            var structured = meaningfulLines
                .Where(IsStructuredFactLine)
                .Take(2)
                .ToList();

            if (structured.Count > 0)
                return Sanitize(string.Join(" | ", structured));

            var firstSentence = meaningfulLines[0];
            if (meaningfulLines.Count > 1 && firstSentence.Length < 50)
                firstSentence = $"{firstSentence} {meaningfulLines[1]}";

            return Sanitize(firstSentence);
        }

        private static bool IsStructuredFactLine(string line)
        {
            if (line.StartsWith("- ", StringComparison.Ordinal) ||
                line.StartsWith("* ", StringComparison.Ordinal))
                return true;

            var colonIndex = line.IndexOf(':');
            return colonIndex > 0 && colonIndex < 32;
        }

        private static bool TryGetSemanticLabel(string? content, out string label)
        {
            var text = content?.Trim() ?? string.Empty;

            if (text.StartsWith("Todo list updated:", StringComparison.OrdinalIgnoreCase))
            {
                label = "Todo";
                return true;
            }

            if (text.StartsWith("Complete task:", StringComparison.OrdinalIgnoreCase) ||
                text.StartsWith("Task completed:", StringComparison.OrdinalIgnoreCase))
            {
                label = "Completion";
                return true;
            }

            if (text.StartsWith("Ask:", StringComparison.OrdinalIgnoreCase) ||
                text.EndsWith("?", StringComparison.Ordinal))
            {
                label = "Question";
                return true;
            }

            label = string.Empty;
            return false;
        }
    }
}
