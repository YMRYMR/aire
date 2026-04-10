using System;
using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Aire.Services
{
    public static partial class ToolCallParser
    {
        private static readonly Regex ToolCallRegex = new(
            @"<(tool_call|tool_code|tool_use|tool)>?\s*((?:(?!</?(?:tool_call|tool_code|tool_use|tool)>)[\s\S])*?)\s*</(?:tool_call|tool_code|tool_use|tool)>",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex ThinkBlockRegex = new(
            @"<think(?:ing)?>\s*[\s\S]*?\s*</think(?:ing)?>",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex CodeFencedJsonRegex = new(
            @"```(?:json)?\r?\n([\s\S]*?)\r?\n[ \t]*```",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex DetailsToolCallRegex = new(
            @"<details>\s*<summary>\s*.*?Tool call:\s*(?<tool>[^<\r\n]+?)\s*</summary>\s*(?<body>[\s\S]*?)</details>",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static string NormalizeBareJsonToolCalls(string response)
        {
            if (response.Contains("<tool_call", StringComparison.OrdinalIgnoreCase))
                return response;

            response = NormalizeDetailsToolCalls(response);

            if (response.Contains("<tool_call", StringComparison.OrdinalIgnoreCase))
                return response;

            var result = CodeFencedJsonRegex.Replace(response, m =>
            {
                var inner = m.Groups[1].Value.Trim();
                if (!HasToolKey(inner)) return m.Value;
                try { JsonDocument.Parse(inner); return $"\n<tool_call>{inner}</tool_call>"; }
                catch (Exception ex)
                {
                    AppLogger.Warn(nameof(ToolCallParser) + ".NormalizeBareJsonToolCalls", "Failed to parse bare JSON tool call in code fence", ex);
                    return m.Value;
                }
            });

            if (result.Contains("<tool_call")) return result;

            for (int i = 0; i < result.Length; i++)
            {
                if (result[i] != '{') continue;

                int lineStart = result.LastIndexOf('\n', i > 0 ? i - 1 : 0) + 1;
                bool atLineStart = true;
                for (int k = lineStart; k < i; k++)
                    if (result[k] != ' ' && result[k] != '\t') { atLineStart = false; break; }
                if (!atLineStart) continue;

                if (!TryExtractBalancedJson(result, i, out var json, out int endIdx)) continue;
                if (!HasToolKey(json)) continue;

                int lineEnd = result.IndexOf('\n', endIdx);
                if (lineEnd < 0) lineEnd = result.Length;
                bool atLineEnd = true;
                for (int k = endIdx + 1; k < lineEnd; k++)
                    if (result[k] != ' ' && result[k] != '\t') { atLineEnd = false; break; }
                if (!atLineEnd) continue;

                try
                {
                    JsonDocument.Parse(json);
                    result = result[..lineStart].TrimEnd() + "\n<tool_call>" + json + "</tool_call>" + result[lineEnd..];
                    break;
                }
                catch (Exception ex)
                {
                    AppLogger.Warn(nameof(ToolCallParser) + ".NormalizeBareJsonToolCalls", "Failed to parse bare JSON tool call in line", ex);
                }
            }

            return result;
        }

        private static string NormalizeDetailsToolCalls(string response)
        {
            return DetailsToolCallRegex.Replace(response, match =>
            {
                string tool = match.Groups["tool"].Value.Trim();
                string body = match.Groups["body"].Value;

                if (string.IsNullOrWhiteSpace(tool))
                {
                    return match.Value;
                }

                var parameters = new Dictionary<string, object?>(StringComparer.Ordinal);
                foreach (string rawLine in body.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
                {
                    string line = rawLine.Trim();
                    if (string.IsNullOrWhiteSpace(line) ||
                        line.Equals("<br>", StringComparison.OrdinalIgnoreCase) ||
                        line.Equals("<br/>", StringComparison.OrdinalIgnoreCase) ||
                        line.Equals("<br />", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    int colonIndex = line.IndexOf(':');
                    if (colonIndex <= 0)
                    {
                        continue;
                    }

                    string key = NormalizeDetailsParameterName(line[..colonIndex]);
                    string value = line[(colonIndex + 1)..].Trim();
                    if (string.IsNullOrWhiteSpace(key))
                    {
                        continue;
                    }

                    parameters[key] = ParseDetailsParameterValue(value);
                }

                var payload = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["tool"] = tool
                };

                foreach ((string key, object? value) in parameters)
                {
                    payload[key] = value;
                }

                return $"\n<tool_call>{JsonSerializer.Serialize(payload)}</tool_call>";
            });
        }

        private static string NormalizeDetailsParameterName(string rawKey)
        {
            return rawKey
                .Trim()
                .Replace(' ', '_')
                .Replace('-', '_')
                .ToLowerInvariant();
        }

        private static object? ParseDetailsParameterValue(string rawValue)
        {
            string value = rawValue.Trim();

            if (int.TryParse(value, out int intValue))
            {
                return intValue;
            }

            if (bool.TryParse(value, out bool boolValue))
            {
                return boolValue;
            }

            if ((value.StartsWith("{") && value.EndsWith("}")) ||
                (value.StartsWith("[") && value.EndsWith("]")))
            {
                try
                {
                    using JsonDocument doc = JsonDocument.Parse(value);
                    return JsonSerializer.Deserialize<object>(doc.RootElement.GetRawText());
                }
                catch
                {
                    // Fall back to plain string if the inline JSON is malformed.
                }
            }

            return value;
        }

        private static bool HasToolKey(string s) =>
            s.Contains("\"tool\"", StringComparison.Ordinal) ||
            s.Contains("\u201Ctool\u201D", StringComparison.Ordinal) ||
            // OpenAI-style content embedding: {"name": "tool_name", "arguments": {...}}
            (s.Contains("\"name\"", StringComparison.Ordinal) &&
             (s.Contains("\"arguments\"",  StringComparison.Ordinal) ||
              s.Contains("\"parameters\"", StringComparison.Ordinal))) ||
            // ReAct / LangChain format: {"action": "tool_name", "action_input": {...}}
            (s.Contains("\"action\"",       StringComparison.Ordinal) &&
             s.Contains("\"action_input\"", StringComparison.Ordinal));

        private static bool TryExtractBalancedJson(string text, int start, out string json, out int endIdx)
        {
            json = string.Empty;
            endIdx = start;
            int depth = 0;
            bool inString = false;
            for (int i = start; i < text.Length; i++)
            {
                char c = text[i];
                if (inString)
                {
                    if (c == '\\') i++;
                    else if (c == '"') inString = false;
                }
                else
                {
                    if (c == '"') inString = true;
                    else if (c == '{') depth++;
                    else if (c == '}' && --depth == 0)
                    {
                        json = text[start..(i + 1)];
                        endIdx = i;
                        return true;
                    }
                }
            }

            return false;
        }

        private static string NormalizeJson(string s)
        {
            var cleaned = s.Trim();

            if (cleaned.StartsWith("```"))
            {
                var startIndex = cleaned.IndexOf('\n');
                if (startIndex == -1) startIndex = 3;
                else startIndex++;

                var endIndex = cleaned.LastIndexOf("```");
                if (endIndex > startIndex)
                    cleaned = cleaned[startIndex..endIndex].Trim();
            }

            return cleaned
                .Replace('\u201C', '"').Replace('\u201D', '"')
                .Replace('\u2018', '\'').Replace('\u2019', '\'')
                .Replace('\u201A', '\'').Replace('\u201B', '\'')
                .Replace('\u2032', '\'').Replace('\u2033', '"')
                .Replace('\uFF02', '"').Replace('\uFF07', '\'')
                .Replace('\uFF1A', ':').Replace('\uFF0C', ',')
                .Replace('\uFF5B', '{').Replace('\uFF5D', '}')
                .Replace('\uFF3B', '[').Replace('\uFF3D', ']');
        }
    }
}
