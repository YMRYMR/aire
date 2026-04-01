using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Aire.Data;

namespace Aire.Providers
{
    /// <summary>
    /// Provider for local Ollama models with native tool-calling support.
    /// Tool schemas are sent via the Ollama API's <c>tools</c> parameter so that
    /// models like qwen2.5-coder, llama3.1, etc. can use them natively.
    /// Native tool-call responses are converted to the &lt;tool_call&gt; text format
    /// that the rest of the app already understands.
    /// </summary>
    public partial class OllamaProvider : BaseAiProvider
    {
        private const int MaxSupportedTimeoutMinutes = 35791;
        private readonly HttpClient _httpClient;
        private TimeSpan _requestTimeout = TimeSpan.FromMinutes(5);
        private string _baseUrl  = "http://localhost:11434";
        private string _apiKey   = string.Empty;

        internal string ConfiguredBaseUrl => _baseUrl;
        internal string ConfiguredApiKey  => _apiKey;
        internal HttpClient ConfiguredHttpClient => _httpClient;

        private static readonly JsonSerializerOptions SerializeOpts = new()
        {
            PropertyNamingPolicy        = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition      = JsonIgnoreCondition.WhenWritingNull
        };

        private static readonly JsonSerializerOptions DeserializeOpts = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        };

        /// <summary>
        /// Creates the provider and initializes its dedicated HTTP client with the default timeout.
        /// </summary>
        public OllamaProvider()
        {
            _httpClient         = new HttpClient();
            _httpClient.Timeout = _requestTimeout;
        }

        public override string ProviderType => "Ollama";
        public override string DisplayName  => "Ollama (Local)";
        protected override ToolCallMode DefaultToolCallMode => ToolCallMode.NativeFunctionCalling;

        protected override ProviderCapabilities GetBaseCapabilities() =>
            ProviderCapabilities.TextChat   |
            ProviderCapabilities.Streaming  |
            ProviderCapabilities.SystemPrompt |
            ProviderCapabilities.ToolCalling;

        public override ProviderFieldHints FieldHints => new()
        {
            ShowApiKey = false,
            ApiKeyRequired = false,
        };

        public override IReadOnlyList<ProviderAction> Actions => new[]
        {
            new ProviderAction
            {
                Id = "ollama-refresh",
                Label = "Refresh Models",
                Placement = ProviderActionPlacement.ModelArea,
            },
            new ProviderAction
            {
                Id = "ollama-download",
                Label = "Download Model",
                Placement = ProviderActionPlacement.ModelArea,
            },
            new ProviderAction
            {
                Id = "ollama-uninstall",
                Label = "Uninstall Model",
                Placement = ProviderActionPlacement.ModelArea,
            },
            new ProviderAction
            {
                Id = "ollama-install",
                Label = "Install Ollama",
                Placement = ProviderActionPlacement.ApiKeyArea,
            },
        };

        private static readonly HttpClient _metaHttp = new() { Timeout = TimeSpan.FromSeconds(8) };

        /// <summary>
        /// Models confirmed at runtime to reject the <c>tools</c> field with a 400 error.
        /// Cached for the lifetime of the process so subsequent requests skip tools immediately.
        /// </summary>
        private static readonly HashSet<string> _noToolsModels = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Tool schemas in Ollama API format, generated from SharedToolDefinitions.</summary>
        private List<object> _toolDefinitions = new();
    }
}
