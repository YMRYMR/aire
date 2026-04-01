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
    /// Result of parsing one AI response into plain text and an optional tool-call payload.
    /// </summary>
    public class ParsedAiResponse
    {
        public string TextContent { get; set; } = string.Empty;
        public ToolCallRequest? ToolCall { get; set; }
        public bool HasToolCall => ToolCall != null;
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

            var allMatches = ToolCallRegex.Matches(response);
            foreach (Match match in allMatches)
            {
                var rawJson = match.Groups[2].Value.Trim();
                if (TryParseToolCallJson(rawJson, out var toolCall))
                {
                    var textBefore = ThinkBlockRegex
                        .Replace(response[..match.Index], string.Empty)
                        .Trim();

                    return new ParsedAiResponse
                    {
                        TextContent = textBefore,
                        ToolCall = toolCall
                    };
                }
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

            var stripped = ToolCallRegex.Replace(response, string.Empty);
            var cleaned = ThinkBlockRegex.Replace(stripped, string.Empty).Trim();

            return new ParsedAiResponse { TextContent = cleaned };
        }
    }
}
