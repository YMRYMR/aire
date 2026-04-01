using System.Collections.Generic;
using System.Text.Json;

namespace Aire.Providers
{
    public partial class OllamaProvider
    {
        private class OllamaChatRequest
        {
            public string Model { get; set; } = string.Empty;
            public List<OllamaMessage> Messages { get; set; } = new();
            public bool Stream { get; set; }
            public OllamaOptions? Options { get; set; }
            public List<object>? Tools { get; set; }
        }

        private class OllamaMessage
        {
            public string Role { get; set; } = string.Empty;
            public string Content { get; set; } = string.Empty;
            public string[]? Images { get; set; }
            public List<OllamaToolCall>? ToolCalls { get; set; }
        }

        private class OllamaOptions
        {
            public double? Temperature { get; set; }
            public int? NumPredict { get; set; }
        }

        private class OllamaToolCall
        {
            public OllamaToolCallFunction Function { get; set; } = new();
        }

        private class OllamaToolCallFunction
        {
            public string Name { get; set; } = string.Empty;
            public JsonElement Arguments { get; set; }
        }

        private class OllamaChatResponse
        {
            public OllamaMessage? Message { get; set; }
            public int? EvalCount { get; set; }
            public int? PromptEvalCount { get; set; }
        }

        private class OllamaStreamChunk
        {
            public OllamaMessage? Message { get; set; }
            public bool Done { get; set; }
        }

        private class OllamaTagsResponse
        {
            public List<OllamaModel> Models { get; set; } = new();
        }

        private class OllamaModel
        {
            public string Name { get; set; } = string.Empty;
            public long Size { get; set; }
            public string Digest { get; set; } = string.Empty;
        }
    }
}
