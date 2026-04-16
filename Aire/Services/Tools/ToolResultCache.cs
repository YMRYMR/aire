using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;

namespace Aire.Services
{
    /// <summary>
    /// In-memory cache for idempotent tool execution results.
    /// Prevents redundant tool calls (e.g., re-reading the same file) within a session.
    /// </summary>
    public sealed class ToolResultCache
    {
        private readonly Dictionary<string, CachedToolResult> _cache = new();
        private readonly TimeSpan _defaultTtl = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Tool names whose results are safe to cache (read-only, idempotent).
        /// </summary>
        private static readonly HashSet<string> CacheableTools = new(StringComparer.OrdinalIgnoreCase)
        {
            "read_file",
            "list_directory",
            "search_files",
            "search_file_content",
            "get_system_info",
            "get_running_processes",
            "get_active_window",
            "get_clipboard",
            "list_browser_tabs",
            "read_browser_tab",
            "get_browser_html",
            "get_browser_cookies",
            "request_context",
            "recall",
        };

        /// <summary>
        /// Checks whether a tool name is eligible for caching.
        /// </summary>
        public static bool IsCacheable(string toolName)
            => CacheableTools.Contains(toolName);

        /// <summary>
        /// Builds a cache key from the tool name and its parameters.
        /// </summary>
        public static string BuildKey(string toolName, JsonElement parameters)
            => $"{toolName}:{Canonicalize(parameters)}";

        private static string Canonicalize(JsonElement parameters)
        {
            if (parameters.ValueKind == JsonValueKind.Undefined)
                return string.Empty;

            using var stream = new MemoryStream();
            using var writer = new Utf8JsonWriter(stream);
            WriteCanonicalValue(writer, parameters);
            writer.Flush();
            return Encoding.UTF8.GetString(stream.ToArray());
        }

        private static void WriteCanonicalValue(Utf8JsonWriter writer, JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                {
                    writer.WriteStartObject();
                    var properties = new List<JsonProperty>();
                    foreach (var property in element.EnumerateObject())
                        properties.Add(property);
                    properties.Sort((left, right) => StringComparer.Ordinal.Compare(left.Name, right.Name));

                    foreach (var property in properties)
                    {
                        writer.WritePropertyName(property.Name);
                        WriteCanonicalValue(writer, property.Value);
                    }

                    writer.WriteEndObject();
                    break;
                }

                case JsonValueKind.Array:
                    writer.WriteStartArray();
                    foreach (var item in element.EnumerateArray())
                        WriteCanonicalValue(writer, item);
                    writer.WriteEndArray();
                    break;

                case JsonValueKind.String:
                    writer.WriteStringValue(element.GetString());
                    break;

                case JsonValueKind.Number:
                    writer.WriteRawValue(element.GetRawText(), skipInputValidation: true);
                    break;

                case JsonValueKind.True:
                    writer.WriteBooleanValue(true);
                    break;

                case JsonValueKind.False:
                    writer.WriteBooleanValue(false);
                    break;

                case JsonValueKind.Null:
                    writer.WriteNullValue();
                    break;
            }
        }

        /// <summary>
        /// Attempts to retrieve a cached result for the given key.
        /// Returns null if not found or expired.
        /// </summary>
        public ToolExecutionResult? TryGet(string key)
        {
            if (_cache.TryGetValue(key, out var cached))
            {
                if (DateTime.UtcNow - cached.Timestamp < _defaultTtl)
                    return cached.Result;
                _cache.Remove(key);
            }
            return null;
        }

        /// <summary>
        /// Stores a tool execution result in the cache.
        /// </summary>
        public void Set(string key, ToolExecutionResult result)
        {
            _cache[key] = new CachedToolResult(result, DateTime.UtcNow);
        }

        /// <summary>
        /// Clears all cached results. Called between conversation turns or on demand.
        /// </summary>
        public void Clear() => _cache.Clear();

        /// <summary>
        /// Number of entries currently in the cache (for diagnostics).
        /// </summary>
        public int Count => _cache.Count;

        private sealed record CachedToolResult(ToolExecutionResult Result, DateTime Timestamp);
    }
}
