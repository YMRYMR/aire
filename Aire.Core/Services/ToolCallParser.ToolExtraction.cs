using System;
using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Aire.Services
{
    public static partial class ToolCallParser
    {
        private static IReadOnlyList<ToolCallRequest> ParseToolCallJson(string raw)
        {
            var results = new List<ToolCallRequest>();

            if (TryParseXmlStyleToolCall(raw, out var xmlResult))
            {
                results.Add(xmlResult);
                return results;
            }

            if (TryExtractTool(NormalizeJson(raw), out var normalizedResult))
            {
                results.Add(normalizedResult);
                return results;
            }

            if (TryParseStructuredActionBlock(raw, out var structuredResult))
            {
                results.Add(structuredResult);
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

        private static bool TryParseXmlStyleToolCall(string raw, out ToolCallRequest result)
        {
            result = new ToolCallRequest();

            if (string.IsNullOrWhiteSpace(raw))
            {
                return false;
            }

            string candidate = raw.Trim();
            if (candidate.StartsWith("{", StringComparison.Ordinal) ||
                candidate.StartsWith("[", StringComparison.Ordinal))
            {
                return false;
            }

            if (!candidate.Contains('>'))
            {
                return false;
            }

            string attrs = string.Empty;
            string body = string.Empty;

            if (candidate.Contains("<", StringComparison.Ordinal))
            {
                Match xmlMatch = Regex.Match(candidate,
                    @"^\s*<(?<tag>tool_calls|tool_call|tool_code|tool_use|tool)(?<attrs>[^>]*)>(?<body>[\s\S]*?)</\k<tag>>\s*$",
                    RegexOptions.IgnoreCase);
                if (xmlMatch.Success)
                {
                    attrs = xmlMatch.Groups["attrs"].Value;
                    body = xmlMatch.Groups["body"].Value;
                }
            }

            if (string.IsNullOrWhiteSpace(body))
            {
                int closeIndex = candidate.IndexOf('>');
                if (closeIndex <= 0)
                {
                    return false;
                }

                attrs = candidate[..closeIndex];
                body = candidate[(closeIndex + 1)..];
            }

            string tool = ExtractXmlAttribute(attrs, "name");
            if (string.IsNullOrWhiteSpace(tool))
            {
                tool = ExtractXmlAttribute(attrs, "tool");
            }

            var explicitPayload = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["tool"] = tool
            };

            bool foundArgument = false;
            foreach (Match argMatch in Regex.Matches(body,
                         @"<arg(?<attrs>[^>]*)>(?<value>[\s\S]*?)</arg>",
                         RegexOptions.IgnoreCase))
            {
                string argAttrs = argMatch.Groups["attrs"].Value;
                string argName = ExtractXmlAttribute(argAttrs, "name");
                if (string.IsNullOrWhiteSpace(argName))
                {
                    argName = ExtractXmlAttribute(argAttrs, "key");
                }

                if (string.IsNullOrWhiteSpace(argName))
                {
                    continue;
                }

                explicitPayload[NormalizeDetailsParameterName(argName)] = ParseDetailsParameterValue(argMatch.Groups["value"].Value);
                foundArgument = true;
            }

            if (!foundArgument)
            {
                foreach (var (key, value) in ExtractSimpleStructuredElements(body))
                {
                    if (string.IsNullOrWhiteSpace(key))
                    {
                        continue;
                    }

                    explicitPayload[NormalizeDetailsParameterName(key)] = value;
                    foundArgument = true;
                }
            }

            if (!foundArgument)
            {
                string trimmedBody = body.Trim();
                if (trimmedBody.StartsWith("{", StringComparison.Ordinal) &&
                    trimmedBody.EndsWith("}", StringComparison.Ordinal))
                {
                    try
                    {
                        using JsonDocument doc = JsonDocument.Parse(trimmedBody);
                        if (doc.RootElement.ValueKind == JsonValueKind.Object)
                        {
                            foreach (JsonProperty property in doc.RootElement.EnumerateObject())
                            {
                                if (property.NameEquals("tool") || property.NameEquals("name") || property.NameEquals("arguments") || property.NameEquals("parameters"))
                                {
                                    continue;
                                }

                                explicitPayload[property.Name] = JsonSerializer.Deserialize<object>(property.Value.GetRawText());
                            }
                        }
                    }
                    catch
                    {
                        explicitPayload["text"] = trimmedBody;
                    }
                }
                else if (!string.IsNullOrWhiteSpace(trimmedBody))
                {
                    explicitPayload["text"] = trimmedBody;
                }
            }

            string normalized = JsonSerializer.Serialize(explicitPayload);
            if (!TryExtractTool(normalized, out result))
            {
                return false;
            }

            return true;
        }

        private static bool TryParseStructuredActionBlock(string raw, out ToolCallRequest result)
        {
            result = new ToolCallRequest();

            if (string.IsNullOrWhiteSpace(raw))
            {
                return false;
            }

            Match match = StructuredActionBlockRegex.Match(raw.Trim());
            if (!match.Success)
            {
                return false;
            }

            string rootTag = match.Groups["tag"].Value;
            string body = match.Groups["body"].Value;
            string tool = InferToolFromStructuredBlock(rootTag, body, out var extras);
            if (string.IsNullOrWhiteSpace(tool))
            {
                return false;
            }

            var payload = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["tool"] = tool
            };

            foreach (var (key, value) in extras)
            {
                payload[key] = value;
            }

            foreach (var (key, value) in ExtractSimpleStructuredElements(body))
            {
                if (!payload.ContainsKey(key))
                {
                    payload[key] = value;
                }
            }

            if (!TryExtractTool(JsonSerializer.Serialize(payload), out result))
            {
                return false;
            }

            return true;
        }

        private static bool IsNestedInsideToolCallBlock(Match structuredMatch, string response)
        {
            foreach (Match toolMatch in ToolCallRegex.Matches(response))
            {
                if (toolMatch.Index <= structuredMatch.Index &&
                    toolMatch.Index + toolMatch.Length >= structuredMatch.Index + structuredMatch.Length)
                {
                    return true;
                }
            }

            return false;
        }

        private static string ExtractXmlAttribute(string attrs, string name)
        {
            if (string.IsNullOrWhiteSpace(attrs))
            {
                return string.Empty;
            }

            Match match = Regex.Match(attrs,
                $@"(?:^|\s){Regex.Escape(name)}\s*=\s*['""](?<value>[^'""]+)['""]",
                RegexOptions.IgnoreCase);
            return match.Success ? match.Groups["value"].Value.Trim() : string.Empty;
        }

        private static IEnumerable<KeyValuePair<string, object?>> ExtractSimpleStructuredElements(string body)
        {
            if (string.IsNullOrWhiteSpace(body))
            {
                yield break;
            }

            foreach (Match element in Regex.Matches(body, @"<(?<name>[A-Za-z0-9_:-]+)>(?<value>[\s\S]*?)</\k<name>>", RegexOptions.IgnoreCase))
            {
                var name = element.Groups["name"].Value;
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                if (name.Equals("tool", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("tool_call", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("tool_calls", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("arg", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                yield return new KeyValuePair<string, object?>(name, ParseDetailsParameterValue(element.Groups["value"].Value));
            }
        }

        private static string InferToolFromStructuredBlock(string rootTag, string body, out Dictionary<string, object?> extras)
        {
            extras = new Dictionary<string, object?>(StringComparer.Ordinal);
            if (string.IsNullOrWhiteSpace(body) && string.IsNullOrWhiteSpace(rootTag))
            {
                return string.Empty;
            }

            var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var kvp in ExtractSimpleStructuredElements(body))
            {
                values[kvp.Key] = kvp.Value;
            }

            values.TryGetValue("action", out var actionValue);
            values.TryGetValue("path", out var pathValue);
            values.TryGetValue("directory", out var directoryValue);
            values.TryGetValue("content", out var contentValue);
            values.TryGetValue("text", out var textValue);
            values.TryGetValue("from", out var fromValue);
            values.TryGetValue("to", out var toValue);
            values.TryGetValue("pattern", out var patternValue);

            var action = actionValue?.ToString()?.Trim().ToLowerInvariant() ?? string.Empty;
            var root = rootTag.Trim().ToLowerInvariant();

            if (root.Contains("folder") || root.Contains("file") || root.Contains("filesystem"))
            {
                switch (action)
                {
                    case "create":
                    case "mkdir":
                    case "make":
                    case "new":
                        if (pathValue != null)
                        {
                            extras["path"] = pathValue;
                        }

                        if (root.Contains("file") &&
                            (contentValue != null || textValue != null || LooksLikeFilePath(pathValue?.ToString())))
                        {
                            if (contentValue != null)
                            {
                                extras["content"] = contentValue;
                            }
                            else if (textValue != null)
                            {
                                extras["content"] = textValue;
                            }
                            return "write_file";
                        }

                        return "create_directory";
                    case "list":
                    case "ls":
                    case "dir":
                    case "show":
                    case "browse":
                        if (pathValue != null)
                        {
                            extras["path"] = pathValue;
                        }
                        else if (directoryValue != null)
                        {
                            extras["path"] = directoryValue;
                        }
                        return extras.ContainsKey("path") ? "list_directory" : string.Empty;
                    case "read":
                    case "cat":
                    case "open":
                        if (pathValue != null)
                        {
                            extras["path"] = pathValue;
                        }
                        return "read_file";
                    case "write":
                    case "save":
                    case "append":
                        if (pathValue != null)
                        {
                            extras["path"] = pathValue;
                        }
                        if (contentValue != null)
                        {
                            extras["content"] = contentValue;
                        }
                        else if (textValue != null)
                        {
                            extras["content"] = textValue;
                        }
                        if (action == "append")
                        {
                            extras["append"] = true;
                        }
                        return "write_file";
                    case "delete":
                    case "remove":
                    case "rm":
                        if (pathValue != null)
                        {
                            extras["path"] = pathValue;
                        }
                        if (root.Contains("folder") || root.Contains("filesystem"))
                        {
                            return LooksLikeFilePath(pathValue?.ToString()) ? "delete_file" : string.Empty;
                        }

                        return "delete_file";
                    case "move":
                    case "rename":
                        if (fromValue != null)
                        {
                            extras["from"] = fromValue;
                        }
                        if (toValue != null)
                        {
                            extras["to"] = toValue;
                        }
                        return "move_file";
                    case "search":
                    case "find":
                        if (directoryValue != null)
                        {
                            extras["directory"] = directoryValue;
                        }
                        else if (pathValue != null)
                        {
                            extras["directory"] = pathValue;
                        }
                        if (patternValue != null)
                        {
                            extras["pattern"] = patternValue;
                        }
                        return string.IsNullOrWhiteSpace(patternValue?.ToString()) ? string.Empty : "search_file_content";
                }
            }

            return string.Empty;
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

        private static bool LooksLikeFilePath(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            string trimmed = path.Trim();
            int lastSlash = Math.Max(trimmed.LastIndexOf('/'), trimmed.LastIndexOf('\\'));
            string lastSegment = lastSlash >= 0 ? trimmed[(lastSlash + 1)..] : trimmed;
            return lastSegment.Contains('.') && !lastSegment.EndsWith(".", StringComparison.Ordinal);
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
