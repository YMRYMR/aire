using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json.Serialization;

namespace Aire.Services
{
    /// <summary>
    /// Service for interacting with Ollama API (model detection, downloads, installation).
    /// </summary>
    public partial class OllamaService : IDisposable
    {
        private readonly HttpClient _httpClient;
        private bool _disposed;

        public OllamaService()
        {
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromMinutes(10)
            };
        }

        /// <summary>
        /// Gets the default Ollama base URL (localhost:11434).
        /// </summary>
        public static string DefaultBaseUrl => "http://localhost:11434";

        #region Data Models

        public class OllamaModel
        {
            [JsonPropertyName("name")]
            public string Name { get; set; } = string.Empty;

            [JsonPropertyName("size")]
            public long Size { get; set; }

            [JsonPropertyName("digest")]
            public string Digest { get; set; } = string.Empty;
        }

        /// <summary>
        /// Capability metadata for a well-known Ollama model.
        /// Tags: "tools" = function/tool calling, "thinking" = reasoning/CoT mode,
        ///       "vision" = image input, "code" = code-focused, "embedding" = embedding-only.
        /// </summary>
        public record OllamaModelMeta(
            string[] Tags,
            bool Recommended,
            string ParamSize = "",
            long SizeBytes = 0);

        private class OllamaTagsResponse
        {
            [JsonPropertyName("models")]
            public List<OllamaModel> Models { get; set; } = new();
        }

        private class OllamaPullRequest
        {
            public string Name { get; set; } = string.Empty;
            public bool Stream { get; set; } = true;
        }

        private class OllamaDeleteRequest
        {
            public string Name { get; set; } = string.Empty;
        }

        public class OllamaPullProgress
        {
            public string Status { get; set; } = string.Empty;
            public string Digest { get; set; } = string.Empty;
            public long Total { get; set; }
            public long Completed { get; set; }
        }

        #endregion
    }
}
