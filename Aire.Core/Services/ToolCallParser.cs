using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Aire.Services
{
    /// <summary>
    /// Normalized tool call extracted from an AI response.
    /// </summary>
    public class ToolCallRequest
    {
        public string Tool { get; set; } = string.Empty;
        public JsonElement Parameters { get; set; }
        public string Description { get; set; } = string.Empty;
        public string RawJson { get; set; } = string.Empty;
    }

    /// <summary>
    /// Result of parsing one AI response into visible text and any normalized tool calls found in order.
    /// </summary>
    public class ParsedAiResponse
    {
        private ToolCallRequest? _toolCall;
        private IReadOnlyList<ToolCallRequest> _toolCalls = Array.Empty<ToolCallRequest>();

        public string TextContent { get; set; } = string.Empty;

        /// <summary>
        /// First parsed tool call for compatibility with older single-tool call sites.
        /// Setting this also updates <see cref="ToolCalls"/> to a single-item list.
        /// </summary>
        public ToolCallRequest? ToolCall
        {
            get => _toolCall;
            set
            {
                _toolCall = value;
                _toolCalls = value == null
                    ? Array.Empty<ToolCallRequest>()
                    : new[] { value };
            }
        }

        /// <summary>
        /// All parsed tool calls in the order they appeared in the response.
        /// Setting this also updates <see cref="ToolCall"/> to the first item.
        /// </summary>
        public IReadOnlyList<ToolCallRequest> ToolCalls
        {
            get => _toolCalls;
            set
            {
                _toolCalls = value ?? Array.Empty<ToolCallRequest>();
                _toolCall = _toolCalls.Count > 0 ? _toolCalls[0] : null;
            }
        }

        public bool HasToolCall => ToolCalls.Count > 0;
    }

    public static partial class ToolCallParser
    {
        /// <summary>
        /// Parses provider output into plain user-facing text plus an optional tool call.
        /// Think blocks and incomplete tool tags are handled here so the rest of the app can work with one normalized result.
        /// </summary>
        /// <param name="response">Raw provider response text.</param>
        /// <returns>A normalized parse result containing visible text and an optional tool call.</returns>
        public static ParsedAiResponse Parse(string response)
        {
            response = NormalizeBareJsonToolCalls(response);
            var collectedToolCalls = new List<(int Index, ToolCallRequest Call)>();
            CollectStructuredActionBlocks(response, collectedToolCalls);

            var allMatches = ToolCallRegex.Matches(response);
            foreach (Match match in allMatches)
            {
                var rawJson = match.Groups[2].Value.Trim();
                foreach (var toolCall in ParseToolCallJson(rawJson))
                {
                    collectedToolCalls.Add((match.Index, toolCall));
                }
            }

            collectedToolCalls.Sort(static (left, right) =>
            {
                int indexComparison = left.Index.CompareTo(right.Index);
                return indexComparison != 0 ? indexComparison : 0;
            });

            var toolCalls = new List<ToolCallRequest>(collectedToolCalls.Count);
            foreach (var (_, toolCall) in collectedToolCalls)
            {
                toolCalls.Add(toolCall);
            }

            var cleaned = ThinkBlockRegex.Replace(
                ToolCallRegex.Replace(
                    StructuredActionBlockRegex.Replace(response, string.Empty),
                    string.Empty),
                string.Empty).Trim();

            if (toolCalls.Count > 0)
            {
                return new ParsedAiResponse
                {
                    TextContent = cleaned,
                    ToolCalls = toolCalls
                };
            }

            if (response.Contains("<tool_call", StringComparison.OrdinalIgnoreCase) &&
                !Regex.IsMatch(response, @"</(?:tool_call|tool_code|tool_use|tool)>", RegexOptions.IgnoreCase))
            {
                // The model left the tag open (e.g. <tool_call{"tool":"read_file",...}) —
                // try to extract valid JSON from the unclosed tag before declaring it cut off.
                var unclosedMatch = Regex.Match(response,
                    @"<(?:tool_call|tool_calls|tool_code|tool_use|tool)>?\s*(\{[\s\S]*\})",
                    RegexOptions.IgnoreCase);
                if (unclosedMatch.Success)
                {
                    var rawJson = unclosedMatch.Groups[1].Value.Trim();
                    var unclosedCalls = ParseToolCallJson(rawJson);
                    if (unclosedCalls.Count > 0)
                    {
                        var textBefore = response.Substring(0, response.IndexOf("<tool_call", StringComparison.OrdinalIgnoreCase)).Trim();
                        return new ParsedAiResponse
                        {
                            TextContent = textBefore,
                            ToolCalls = unclosedCalls
                        };
                    }
                }

                return new ParsedAiResponse
                {
                    TextContent = "⚠️ The response was cut off before the tool call could complete (max_tokens limit reached). " +
                                  "Try asking the model to break the task into smaller steps, or increase max_tokens in Settings."
                };
            }

            return new ParsedAiResponse { TextContent = cleaned };
        }

        private static readonly Regex StructuredActionBlockRegex = new(
            @"<(?<tag>folder_structure|file_structure|filesystem_structure|filesystem|file_action)(?<attrs>[^>]*)>(?<body>[\s\S]*?)</\k<tag>>",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static void CollectStructuredActionBlocks(string response, List<(int Index, ToolCallRequest Call)> toolCalls)
        {
            if (string.IsNullOrWhiteSpace(response))
            {
                return;
            }

            var matches = StructuredActionBlockRegex.Matches(response);
            if (matches.Count == 0)
            {
                return;
            }

            foreach (Match match in matches)
            {
                if (TryParseStructuredActionBlock(match.Value, out var structuredCall))
                {
                    toolCalls.Add((match.Index, structuredCall));
                }
            }
        }
    }
}
