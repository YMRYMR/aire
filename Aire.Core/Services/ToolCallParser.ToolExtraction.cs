using System;
using System.Diagnostics;
using System.Text.Json;

namespace Aire.Services
{
    public static partial class ToolCallParser
    {
        private static IReadOnlyList<ToolCallRequest> ParseToolCallJson(string raw)
        {
            var results = new List<ToolCallRequest>();

            if (TryExtractTool(NormalizeJson(raw), out var normalizedResult))
            {
                results.Add(normalizedResult);
                return results;
            }

            var trimmed = raw.TrimEnd();
            int lastBrace = trimmed.LastIndexOf('}');
            if (lastBrace >= 0 && lastBrace < trimmed.Length - 1)
            {
                var candidate = NormalizeJson(trimmed[..(lastBrace + 1)]);
                if (TryExtractTool(candidate, out var trailingResult))
                {
                    results.Add(trailingResult);
                    return results;
                }
            }

            var normalized = NormalizeJson(raw);
            if (!normalized.StartsWith('['))
                return results;

            try
            {
                using var arr = JsonDocument.Parse(normalized);
                if (arr.RootElement.ValueKind != JsonValueKind.Array)
                    return results;

                foreach (var element in arr.RootElement.EnumerateArray())
                {
                    if (TryExtractTool(element.GetRawText(), out var arrayResult))
                        results.Add(arrayResult);
                }
            }
            catch (Exception ex)
            {
                AppLogger.Warn(nameof(ToolCallParser) + ".ParseToolCallJson", "Failed to parse tool-call JSON", ex);
            }

            return results;
        }

        private static bool TryParseToolCallJson(string raw, out ToolCallRequest result)
        {
            var results = ParseToolCallJson(raw);
            result = results.Count > 0 ? results[0] : new ToolCallRequest();
            return results.Count > 0;
        }

        private static bool TryExtractTool(string json, out ToolCallRequest result)
        {
            result = new ToolCallRequest();
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (root.ValueKind != JsonValueKind.Object) return false;

                // ── Aire-native format: {"tool": "name", ...params} ──────────
                if (root.TryGetProperty("tool", out var toolProp))
                {
                    var tool = toolProp.GetString() ?? string.Empty;
                    if (string.IsNullOrEmpty(tool)) return false;

                    var liveDoc = JsonDocument.Parse(json);
                    var liveRoot = liveDoc.RootElement;
                    result = new ToolCallRequest
                    {
                        Tool        = tool,
                        Parameters  = liveRoot.Clone(),
                        Description = BuildDescription(tool, liveRoot),
                        RawJson     = json
                    };
                    return true;
                }

                // ── OpenAI-style content embedding ────────────────────────────
                // Some models (e.g. MiMo, certain OpenRouter-served models) embed
                // tool calls in the content field using OpenAI's own schema:
                //   {"name": "tool_name", "arguments": {...}}  or
                //   {"name": "tool_name", "arguments": "{...}"}  (double-encoded)
                //   {"name": "tool_name", "parameters": {...}}
                if (root.TryGetProperty("name", out var nameProp))
                {
                    var toolName = nameProp.GetString() ?? string.Empty;
                    if (string.IsNullOrEmpty(toolName)) return false;

                    // Resolve arguments: may be an object or a double-encoded JSON string
                    string argsJson = "{}";
                    if (root.TryGetProperty("arguments",  out var argsEl) ||
                        root.TryGetProperty("parameters", out argsEl))
                    {
                        if (argsEl.ValueKind == JsonValueKind.String)
                        {
                            // Double-encoded: the model serialised the args as a string
                            argsJson = argsEl.GetString() ?? "{}";
                        }
                        else if (argsEl.ValueKind == JsonValueKind.Object)
                        {
                            argsJson = argsEl.GetRawText();
                        }
                    }

                    // Flatten into Aire's format: {"tool": "name", ...params}
                    var normalized = FlattenOpenAiStyleToolCall(toolName, argsJson);
                    if (normalized == null) return false;

                    var liveDoc = JsonDocument.Parse(normalized);
                    var liveRoot = liveDoc.RootElement;
                    result = new ToolCallRequest
                    {
                        Tool        = toolName,
                        Parameters  = liveRoot.Clone(),
                        Description = BuildDescription(toolName, liveRoot),
                        RawJson     = normalized
                    };
                    return true;
                }

                // ── ReAct / LangChain format ──────────────────────────────────
                // {"action": "tool_name", "action_input": {...}}  or
                // {"action": "tool_name", "action_input": "single string arg"}
                if (root.TryGetProperty("action", out var actionProp))
                {
                    var toolName = actionProp.GetString() ?? string.Empty;
                    if (string.IsNullOrEmpty(toolName)) return false;

                    string argsJson = "{}";
                    if (root.TryGetProperty("action_input", out var inputEl))
                    {
                        if (inputEl.ValueKind == JsonValueKind.Object)
                        {
                            argsJson = inputEl.GetRawText();
                        }
                        else if (inputEl.ValueKind == JsonValueKind.String)
                        {
                            // May be a double-encoded JSON object or a plain string value.
                            var raw = inputEl.GetString() ?? string.Empty;
                            try
                            {
                                using var probe = JsonDocument.Parse(raw);
                                argsJson = probe.RootElement.ValueKind == JsonValueKind.Object
                                    ? raw
                                    : "{}";
                            }
                            catch { argsJson = "{}"; }
                        }
                    }

                    var normalized = FlattenOpenAiStyleToolCall(toolName, argsJson);
                    if (normalized == null) return false;

                    var liveDoc = JsonDocument.Parse(normalized);
                    var liveRoot = liveDoc.RootElement;
                    result = new ToolCallRequest
                    {
                        Tool        = toolName,
                        Parameters  = liveRoot.Clone(),
                        Description = BuildDescription(toolName, liveRoot),
                        RawJson     = normalized
                    };
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                AppLogger.Warn(nameof(ToolCallParser) + ".TryExtractTool", "Failed to extract tool from JSON", ex);
                return false;
            }
        }

        /// <summary>
        /// Converts an OpenAI-style tool call object
        /// <c>{"name":"tool","arguments":{...}}</c> into Aire's flat format
        /// <c>{"tool":"tool",...params}</c> so parameter accessors work unchanged.
        /// </summary>
        private static string? FlattenOpenAiStyleToolCall(string toolName, string argumentsJson)
        {
            try
            {
                using var argsDoc = JsonDocument.Parse(argumentsJson);
                if (argsDoc.RootElement.ValueKind != JsonValueKind.Object)
                    return $"{{\"tool\":{JsonSerializer.Serialize(toolName)}}}";

                var sb = new System.Text.StringBuilder();
                sb.Append("{\"tool\":");
                sb.Append(JsonSerializer.Serialize(toolName));
                foreach (var prop in argsDoc.RootElement.EnumerateObject())
                {
                    sb.Append(',');
                    sb.Append(JsonSerializer.Serialize(prop.Name));
                    sb.Append(':');
                    sb.Append(prop.Value.GetRawText());
                }
                sb.Append('}');
                return sb.ToString();
            }
            catch (Exception ex)
            {
                AppLogger.Warn(nameof(ToolCallParser) + ".FlattenOpenAiStyleToolCall", $"Failed to flatten OpenAI-style tool call '{toolName}'", ex);
                return null;
            }
        }

        private static string GetStr(JsonElement el, string prop)
        {
            if (!el.TryGetProperty(prop, out var v)) return string.Empty;
            return v.ValueKind == JsonValueKind.String ? v.GetString() ?? string.Empty : v.GetRawText();
        }
    }
}
