using System.IO;
using System.Text;
using Aire.Data;
using Aire.Domain.Providers;
using Aire.Services;

namespace Aire.Providers
{
    /// <summary>
    /// Provider-agnostic chat message used throughout Aire's orchestration layer.
    /// Providers convert this shape into their own HTTP payloads.
    /// </summary>
    public class ChatMessage
    {
        public string Role { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string? ImagePath { get; set; }
        public byte[]? ImageBytes { get; set; }
        public string? ImageMimeType { get; set; }
        public List<MessageAttachment>? Attachments { get; set; }
        public bool PreferPromptCache { get; set; }
    }

    /// <summary>
    /// Runtime configuration passed into an AI provider after the persisted provider row is normalized.
    /// </summary>
    public class ProviderConfig
    {
        public string ApiKey { get; set; } = string.Empty;
        public string? BaseUrl { get; set; }
        public string Model { get; set; } = string.Empty;
        public double Temperature { get; set; } = 0.7;
        public int MaxTokens { get; set; } = 16384;
        public int TimeoutMinutes { get; set; } = 5;
        public List<string>? ModelCapabilities { get; set; }
        public List<string>? EnabledToolCategories { get; set; }

        /// <summary>
        /// When true, capability-test requests must not inject native tool schemas into the request.
        /// This prevents hundreds of kilobytes of function definitions being sent on every test turn.
        /// </summary>
        public bool SkipNativeTools { get; set; }
    }

    /// <summary>
    /// Normalized provider response returned to the orchestration layer.
    /// </summary>
    public class AiResponse
    {
        public string Content { get; set; } = string.Empty;
        public int TokensUsed { get; set; }
        public TimeSpan Duration { get; set; }
        public bool IsSuccess { get; set; } = true;
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// How a provider exposes tool calls to Aire.
    /// </summary>
    public enum ToolCallMode
    {
        TextBased,
        NativeFunctionCalling,
        Unsupported,
    }

    /// <summary>
    /// The format in which a model outputs tool calls inside its response content.
    /// Resolved from the <c>toolformat:</c> model-capability tag; providers set their own default.
    /// </summary>
    public enum ToolOutputFormat
    {
        /// <summary>Standard OpenAI <c>tool_calls</c> response field — no in-content tag needed.</summary>
        NativeToolCalls,
        /// <summary>Qwen/Hermes style: <c>&lt;tool_call&gt;{"name":"…","arguments":{…}}&lt;/tool_call&gt;</c> in content.</summary>
        Hermes,
        /// <summary>LangChain ReAct style: <c>{"action":"…","action_input":{…}}</c> in content.</summary>
        React,
        /// <summary>Aire's own text format: <c>&lt;tool_call&gt;{"tool":"…",…params}&lt;/tool_call&gt;</c> in content.</summary>
        AireText,
    }

    /// <summary>
    /// Feature flags advertised by a provider or narrowed by the selected model.
    /// </summary>
    [Flags]
    public enum ProviderCapabilities
    {
        None         = 0,
        TextChat     = 1 << 0,
        Streaming    = 1 << 1,
        ImageInput   = 1 << 2,
        ToolCalling  = 1 << 3,
        SystemPrompt = 1 << 4,
    }

    /// <summary>
    /// Contract implemented by every AI provider supported by Aire.
    /// </summary>
    public interface IAiProvider
    {
        string ProviderType { get; }
        string DisplayName { get; }
        ProviderCapabilities Capabilities { get; }
        ToolCallMode ToolCallMode { get; }
        /// <summary>The format in which this model emits tool calls inside its response content.</summary>
        ToolOutputFormat ToolOutputFormat { get; }
        bool Has(ProviderCapabilities cap);
        /// <summary>Initializes the provider with runtime configuration derived from persisted settings.</summary>
        /// <param name="config">Normalized provider configuration to use for subsequent calls.</param>
        void Initialize(ProviderConfig config);
        /// <summary>Sends a complete non-streaming chat request.</summary>
        /// <param name="messages">Conversation history in Aire's provider-agnostic message format.</param>
        /// <param name="cancellationToken">Cancellation token for the provider request.</param>
        /// <returns>A normalized provider response.</returns>
        Task<AiResponse> SendChatAsync(IEnumerable<ChatMessage> messages, CancellationToken cancellationToken = default);

        /// <summary>
        /// Adjusts provider settings for lightweight capability testing: prevents native tool
        /// schemas from being injected (which can add 100k+ tokens per request) and caps the
        /// maximum response length to a value suitable for a single tool-call reply.
        /// Call this once before running the capability test suite on a provider instance.
        /// </summary>
        void PrepareForCapabilityTesting();
        /// <summary>
        /// Controls whether tool schemas and the tool system prompt are injected into requests.
        /// When disabled, the provider sends plain chat messages with no tool metadata.
        /// </summary>
        void SetToolsEnabled(bool enabled);
        /// <summary>
        /// Restricts tool exposure to the given categories in addition to the model's own capability limits.
        /// </summary>
        void SetEnabledToolCategories(IEnumerable<string>? categories);
        /// <summary>Streams chat output as text chunks when the provider supports streaming.</summary>
        /// <param name="messages">Conversation history in Aire's provider-agnostic message format.</param>
        /// <param name="cancellationToken">Cancellation token for the provider request.</param>
        /// <returns>An async stream of text chunks.</returns>
        IAsyncEnumerable<string> StreamChatAsync(IEnumerable<ChatMessage> messages, CancellationToken cancellationToken = default);
        /// <summary>Checks whether the current provider configuration can successfully talk to the remote API.</summary>
        /// <param name="cancellationToken">Cancellation token for the validation request.</param>
        /// <returns>A validation result indicating whether the configuration is usable, and the reason when it is not.</returns>
        Task<ProviderValidationResult> ValidateConfigurationAsync(CancellationToken cancellationToken = default);
        /// <summary>Returns quota or token-usage information when the provider exposes it.</summary>
        /// <param name="cancellationToken">Cancellation token for the usage lookup.</param>
        /// <returns>Usage details, or <see langword="null"/> when unavailable.</returns>
        Task<TokenUsage?> GetTokenUsageAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Builds the system prompt to inject for this provider's next request.
        /// Providers override this to select a compact vs verbose prompt, apply category filtering,
        /// and append the model list / mode / MCP sections.
        /// The default implementation mirrors the pre-optimization behaviour (no category filtering,
        /// verbose prompts) so that test fakes and non-<see cref="BaseAiProvider"/> implementations
        /// continue to compile without changes.
        /// </summary>
        /// <param name="modelListSection">Model-switch section already rendered by the UI coordinator.</param>
        /// <param name="modePromptSection">Assistant mode section, or <see langword="null"/>.</param>
        /// <param name="mcpSection">Pre-rendered MCP tool section, or <see langword="null"/>.</param>
        /// <returns>The complete system prompt string.</returns>
        string BuildToolSystemPrompt(string modelListSection, string? modePromptSection, string? mcpSection)
        {
            var basePrompt = ToolOutputFormat switch
            {
                ToolOutputFormat.Hermes          => FileSystemSystemPrompt.HermesToolCallingText,
                ToolOutputFormat.React           => FileSystemSystemPrompt.ReactToolCallingText,
                ToolOutputFormat.NativeToolCalls => FileSystemSystemPrompt.NativeToolCallingText,
                _                                => FileSystemSystemPrompt.Text,
            };
            var sb = new StringBuilder(basePrompt);
            if (!string.IsNullOrEmpty(modelListSection)) sb.Append(modelListSection);
            if (!string.IsNullOrWhiteSpace(modePromptSection)) sb.Append(modePromptSection);
            if (!string.IsNullOrWhiteSpace(mcpSection)) sb.Append(mcpSection);
            return sb.ToString();
        }
    }

    /// <summary>
    /// Shared base class for providers that fit Aire's common configuration and capability model.
    /// </summary>
    public abstract class BaseAiProvider : IAiProvider, IProviderMetadata
    {
        protected ProviderConfig Config { get; private set; } = new();

        public abstract string ProviderType { get; }
        public abstract string DisplayName { get; }
        public ProviderCapabilities Capabilities
        {
            get
            {
                var caps = GetBaseCapabilities();
                if (Config?.ModelCapabilities != null && Config.ModelCapabilities.Count > 0)
                {
                    var mcSet = new HashSet<string>(Config.ModelCapabilities, StringComparer.OrdinalIgnoreCase);
                    if (!mcSet.Contains("vision") && !mcSet.Contains("imageinput"))
                        caps &= ~ProviderCapabilities.ImageInput;
                    if (!mcSet.Contains("tools") && !mcSet.Contains("toolcalling"))
                        caps &= ~ProviderCapabilities.ToolCalling;
                }
                return caps;
            }
        }

        protected abstract ProviderCapabilities GetBaseCapabilities();

        /// <summary>
        /// Returns the maximum output tokens the specified model can produce.
        /// Subclasses override to enforce model-specific limits (e.g. z.ai caps
        /// at 4095, Claude Haiku at 8192).  The default returns <see cref="int.MaxValue"/>
        /// so the user's configured value is used as-is when no override exists.
        /// </summary>
        protected virtual int GetModelMaxOutputTokens(string modelId) => int.MaxValue;

        /// <summary>
        /// Effective max_tokens for API requests: the configured value clamped to
        /// the current model's documented output ceiling.
        /// </summary>
        protected int? EffectiveMaxTokens
        {
            get
            {
                if (Config.MaxTokens <= 0) return null;
                return Math.Min(Config.MaxTokens, GetModelMaxOutputTokens(Config.Model));
            }
        }

        protected virtual ToolCallMode DefaultToolCallMode => ToolCallMode.TextBased;
        protected virtual ToolOutputFormat DefaultToolOutputFormat => ToolOutputFormat.AireText;

        public ToolCallMode ToolCallMode
        {
            get
            {
                if (Config?.ModelCapabilities != null)
                {
                    foreach (var cap in Config.ModelCapabilities)
                    {
                        if (cap.StartsWith("toolcallmode:", StringComparison.OrdinalIgnoreCase))
                        {
                            var mode = cap[13..];
                            if (mode.Equals("native", StringComparison.OrdinalIgnoreCase))
                                return ToolCallMode.NativeFunctionCalling;
                            if (mode.Equals("text", StringComparison.OrdinalIgnoreCase))
                                return ToolCallMode.TextBased;
                            if (mode.Equals("unsupported", StringComparison.OrdinalIgnoreCase))
                                return ToolCallMode.Unsupported;
                        }
                    }
                }
                return DefaultToolCallMode;
            }
        }

        public ToolOutputFormat ToolOutputFormat
        {
            get
            {
                if (Config?.ModelCapabilities != null)
                {
                    foreach (var cap in Config.ModelCapabilities)
                    {
                        if (cap.StartsWith("toolformat:", StringComparison.OrdinalIgnoreCase))
                        {
                            var fmt = cap[11..];
                            if (fmt.Equals("hermes",   StringComparison.OrdinalIgnoreCase)) return ToolOutputFormat.Hermes;
                            if (fmt.Equals("react",    StringComparison.OrdinalIgnoreCase)) return ToolOutputFormat.React;
                            if (fmt.Equals("native",   StringComparison.OrdinalIgnoreCase)) return ToolOutputFormat.NativeToolCalls;
                            if (fmt.Equals("text",     StringComparison.OrdinalIgnoreCase)) return ToolOutputFormat.AireText;
                            if (fmt.Equals("airetext", StringComparison.OrdinalIgnoreCase)) return ToolOutputFormat.AireText;
                        }
                    }
                }
                return DefaultToolOutputFormat;
            }
        }

        public bool Has(ProviderCapabilities cap) => (Capabilities & cap) == cap;
        public bool SupportsImages => Has(ProviderCapabilities.ImageInput);

        /// <summary>
        /// Stores the normalized configuration that derived providers will use for requests.
        /// </summary>
        /// <param name="config">Runtime provider configuration.</param>
        public virtual void Initialize(ProviderConfig config)
        {
            Config = config ?? throw new ArgumentNullException(nameof(config));
        }

        public void PrepareForCapabilityTesting()
        {
            Config.SkipNativeTools = true;
            // Cap output tokens: a tool-call response is a few dozen tokens at most.
            // Many models (e.g. Claude Haiku) reject max_tokens > their own ceiling;
            // 1024 is safe for every currently supported model.
            if (Config.MaxTokens > 1024)
                Config.MaxTokens = 1024;
        }

        public void SetToolsEnabled(bool enabled)
        {
            Config.SkipNativeTools = !enabled;
        }

        public void SetEnabledToolCategories(IEnumerable<string>? categories)
        {
            Config.EnabledToolCategories = categories?.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            Config.SkipNativeTools = Config.EnabledToolCategories is { Count: 0 };
        }

        public abstract Task<AiResponse> SendChatAsync(IEnumerable<ChatMessage> messages, CancellationToken cancellationToken = default);

        public virtual IAsyncEnumerable<string> StreamChatAsync(IEnumerable<ChatMessage> messages, CancellationToken cancellationToken = default)
        {
            return AsyncEnumerable.Empty<string>();
        }

        public virtual async Task<ProviderValidationResult> ValidateConfigurationAsync(CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(Config.ApiKey))
            {
                AppLogger.Warn($"{GetType().Name}.ValidateConfiguration", "ApiKey is empty; validation skipped");
                return ProviderValidationResult.Fail("API key is required.");
            }
            try
            {
                var testMessages = new[] { new ChatMessage { Role = "system", Content = "test" } };
                await SendChatAsync(testMessages, cancellationToken).ConfigureAwait(false);
                return ProviderValidationResult.Ok();
            }
            catch (Exception ex)
            {
                AppLogger.Warn($"{GetType().Name}.ValidateConfiguration", "Validation failed", ex);
                return ProviderValidationResult.Fail("Configuration validation failed.");
            }
        }

        /// <summary>
        /// Loads image bytes from disk for providers that accept inline binary image content.
        /// </summary>
        /// <param name="imagePath">Absolute or relative image path.</param>
        /// <returns>The file bytes, or <see langword="null"/> when the file is missing.</returns>
        protected byte[]? LoadImageBytes(string? imagePath)
        {
            if (string.IsNullOrEmpty(imagePath) || !File.Exists(imagePath))
                return null;
            return File.ReadAllBytes(imagePath);
        }

        /// <summary>
        /// Guesses a MIME type for a persisted image path when the provider API needs one.
        /// </summary>
        /// <param name="imagePath">Image file path.</param>
        /// <returns>A best-effort MIME type, or <see langword="null"/> when the path is missing.</returns>
        protected string? GuessMimeType(string? imagePath)
        {
            if (imagePath == null) return null;
            return Path.GetExtension(imagePath).ToLowerInvariant() switch
            {
                ".png"            => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".bmp"            => "image/bmp",
                ".gif"            => "image/gif",
                ".webp"           => "image/webp",
                ".svg"            => "image/svg+xml",
                _                 => "application/octet-stream"
            };
        }

        public virtual Task<TokenUsage?> GetTokenUsageAsync(CancellationToken ct)
            => Task.FromResult<TokenUsage?>(null);

        /// <summary>
        /// When <see langword="true"/>, this provider requests compact (short) tool descriptions
        /// in the native schema payload. Models that handle tools well need far less coaching.
        /// Defaults to <see langword="false"/> (full verbose descriptions).
        /// Override to <see langword="true"/> in providers like OpenAI, Anthropic, and Gemini
        /// where models reliably infer correct usage from the parameter schema alone.
        /// </summary>
        protected virtual bool PreferCompactToolDescriptions => false;

        /// <inheritdoc/>
        public virtual string BuildToolSystemPrompt(string modelListSection, string? modePromptSection, string? mcpSection)
        {
            var enabledCategories = Config.EnabledToolCategories;

            string basePrompt = ToolOutputFormat switch
            {
                ToolOutputFormat.Hermes          => FileSystemSystemPrompt.HermesToolCallingText,
                ToolOutputFormat.React           => FileSystemSystemPrompt.ReactToolCallingText,
                ToolOutputFormat.NativeToolCalls => PreferCompactToolDescriptions
                                                    ? FileSystemSystemPrompt.BuildNativeCompact(enabledCategories)
                                                    : FileSystemSystemPrompt.NativeToolCallingText,
                _                                => FileSystemSystemPrompt.BuildTextBased(enabledCategories),
            };

            var sb = new StringBuilder(basePrompt.Length + (modelListSection?.Length ?? 0) + 256);
            sb.Append(basePrompt);
            if (!string.IsNullOrEmpty(modelListSection))
                sb.Append(modelListSection);
            if (!string.IsNullOrWhiteSpace(modePromptSection))
                sb.Append(modePromptSection);
            if (!string.IsNullOrWhiteSpace(mcpSection))
                sb.Append(mcpSection);
            return sb.ToString();
        }

        public virtual ProviderFieldHints FieldHints => new();
        public virtual IReadOnlyList<ProviderAction> Actions => Array.Empty<ProviderAction>();
        public virtual List<ModelDefinition> GetDefaultModels() => ModelCatalog.GetDefaults(ProviderType);
        public virtual Task<List<ModelDefinition>?> FetchLiveModelsAsync(string? apiKey, string? baseUrl, CancellationToken ct)
            => Task.FromResult<List<ModelDefinition>?>(null);
    }
}
