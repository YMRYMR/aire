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
            var cleaned = ThinkBlockRegex.Replace(ToolCallRegex.Replace(response, string.Empty), string.Empty).Trim();

            var allMatches = ToolCallRegex.Matches(response);
            var toolCalls = new List<ToolCallRequest>();
            foreach (Match match in allMatches)
            {
                var rawJson = match.Groups[2].Value.Trim();
                foreach (var toolCall in ParseToolCallJson(rawJson))
                {
                    toolCalls.Add(toolCall);
                }
            }

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
                return new ParsedAiResponse
                {
                    TextContent = "⚠️ The response was cut off before the tool call could complete (max_tokens limit reached). " +
                                  "Try asking the model to break the task into smaller steps, or increase max_tokens in Settings."
                };
            }

            return new ParsedAiResponse { TextContent = cleaned };
        }
    }
}
