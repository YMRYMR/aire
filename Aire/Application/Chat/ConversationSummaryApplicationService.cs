using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Aire.Providers;
using Aire.Services;

namespace Aire.AppLayer.Chat
{
    /// <summary>
    /// Builds a compact synthetic summary for older conversation turns that were trimmed
    /// out of the active provider context.
    /// </summary>
    public sealed class ConversationSummaryApplicationService
    {
        private readonly ITokenEstimator? _tokenEstimator;

        /// <summary>
        /// Initializes a new instance of the <see cref="ConversationSummaryApplicationService"/> class
        /// without a token estimator (falls back to character-based truncation).
        /// </summary>
        public ConversationSummaryApplicationService()
            : this(null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ConversationSummaryApplicationService"/> class
        /// with a token estimator for token-aware truncation.
        /// </summary>
        /// <param name="tokenEstimator">Optional token estimator; if null, token-aware features are disabled.</param>
        public ConversationSummaryApplicationService(ITokenEstimator? tokenEstimator)
        {
            _tokenEstimator = tokenEstimator;
        }

        private const string SummaryHeader = "Conversation summary of earlier omitted context:";

        private static IEnumerable<IReadOnlyList<ChatMessage>> GroupConsecutiveByRole(IReadOnlyList<ChatMessage> messages)
        {
            if (messages.Count == 0) yield break;
            var currentGroup = new List<ChatMessage> { messages[0] };
            var currentLabel = GetRoleLabel(messages[0]);
            for (int i = 1; i < messages.Count; i++)
            {
                var label = GetRoleLabel(messages[i]);
                if (label == currentLabel)
                {
                    currentGroup.Add(messages[i]);
                }
                else
                {
                    yield return currentGroup;
                    currentGroup = new List<ChatMessage> { messages[i] };
                    currentLabel = label;
                }
            }
            yield return currentGroup;
        }

        private List<(string Label, List<string> Items)> CollectGroups(IReadOnlyList<ChatMessage> nonSystem)
        {
            var groups = new List<(string Label, List<string> Items)>();
            foreach (var group in GroupConsecutiveByRole(nonSystem))
            {
                var items = group
                    .Select(BuildSnippet)
                    .Where(text => !string.IsNullOrWhiteSpace(text))
                    .Distinct(StringComparer.Ordinal)
                    .Take(3)
                    .ToList();
                if (items.Count == 0) continue;
                var label = GetRoleLabel(group[0]);
                groups.Add((label, items));
            }
            return groups;
        }

        private string BuildSummaryFromGroups(List<(string Label, List<string> Items)> groups)
        {
            var sb = new StringBuilder();
            sb.AppendLine(SummaryHeader);
            foreach (var (label, items) in groups)
            {
                sb.Append(label).Append(": ").AppendLine(string.Join(" | ", items));
            }
            return sb.ToString().Trim();
        }

        private string TruncateByGroups(List<(string Label, List<string> Items)> groups, int maxTokens, string? modelId)
        {
            // Start with all groups
            var mutableGroups = new List<(string Label, List<string> Items)>(groups);
            
            while (mutableGroups.Count > 0)
            {
                var candidate = BuildSummaryFromGroups(mutableGroups);
                int tokens = _tokenEstimator!.EstimateTokens(candidate, modelId);
                if (tokens <= maxTokens)
                    return candidate;
                // Remove the last group
                mutableGroups.RemoveAt(mutableGroups.Count - 1);
            }
            
            // No groups left, only header
            var fallbackText = BuildSummaryFromGroups(mutableGroups);
            // Character truncation as last resort
            int targetChars = Math.Max(0, maxTokens * 4);
            if (fallbackText.Length > targetChars)
                fallbackText = fallbackText[..Math.Max(0, targetChars - 1)].TrimEnd() + "…";
            return fallbackText;
        }

        public ChatMessage? BuildSummaryMessage(
            IReadOnlyList<ChatMessage> omittedMessages,
            int maxCharacters = 900,
            int? maxTokens = null,
            string? modelId = null)
        {
            if (omittedMessages.Count == 0 || maxCharacters < 120)
                return null;

            var nonSystem = omittedMessages
                .Where(m => !string.Equals(m.Role, "system", StringComparison.OrdinalIgnoreCase))
                .ToList();

            var groups = CollectGroups(nonSystem);
            var text = BuildSummaryFromGroups(groups);
            if (text.Length <= SummaryHeader.Length)
                return null;

            if (text.Length > maxCharacters)
                text = text[..Math.Max(0, maxCharacters - 1)].TrimEnd() + "…";

            // Token‑based truncation (if enabled)
            if (maxTokens.HasValue && _tokenEstimator != null)
            {
                int tokens = _tokenEstimator.EstimateTokens(text, modelId);
                if (tokens > maxTokens.Value)
                {
                    // Smarter truncation: remove whole groups from the end until token limit is satisfied.
                    text = TruncateByGroups(groups, maxTokens.Value, modelId);
                }
            }

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
            var content = message.Content ?? string.Empty;

            // 1. Tool call detection
            if (content.Contains("<tool_call>"))
            {
                var match = Regex.Match(content, "\"tool\"\\s*:\\s*\"([^\"]+)\"");
                if (match.Success)
                {
                    string toolName = match.Groups[1].Value;
                    // Try to extract first parameter for context
                    var paramMatch = Regex.Match(content, "\"([^\"]+)\"\\s*:\\s*\"([^\"]*)\"");
                    string paramDesc = paramMatch.Success ? $"{paramMatch.Groups[1].Value}=\"{paramMatch.Groups[2].Value}\"" : "";
                    return $"Called {toolName}" + (string.IsNullOrEmpty(paramDesc) ? "" : $" ({paramDesc})");
                }
                return "Tool call";
            }

            // 2. Code block detection
            if (content.Contains("```"))
            {
                var lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                string? language = null;
                foreach (var line in lines)
                {
                    if (line.StartsWith("```"))
                    {
                        language = line.Length > 3 ? line.Substring(3).Trim() : null;
                        break;
                    }
                }
                // Take first non-empty line inside the code block (simplistic)
                var codeLines = content.Split(new[] { '\r', '\n' });
                bool inCodeBlock = false;
                foreach (var line in codeLines)
                {
                    var trimmed = line.Trim();
                    if (trimmed.StartsWith("```"))
                    {
                        inCodeBlock = !inCodeBlock;
                        continue;
                    }
                    if (inCodeBlock && trimmed.Length > 0)
                    {
                        return $"Code{(language != null ? " " + language : "")}: {trimmed}";
                    }
                }
            }

            // 3. Original structured fact detection
            var meaningfulLines = content
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Trim())
                .Where(line => line.Length > 0)
                .ToList();

            if (meaningfulLines.Count == 0)
                return string.Empty;

            var structured = meaningfulLines
                .Where(IsStructuredFactLine)
                .Take(3) // keep a few more items
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
