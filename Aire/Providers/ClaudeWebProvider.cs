using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Aire.Data;
using Aire.Domain.Providers;

namespace Aire.Providers
{
    /// <summary>
    /// Claude.ai provider that uses the browser session established via WebView login.
    /// </summary>
    public class ClaudeWebProvider : BaseAiProvider
    {
        private string? _cachedOrgUuid;

        public override string ProviderType => "ClaudeWeb";
        public override string DisplayName => "Claude.ai";

        protected override ProviderCapabilities GetBaseCapabilities() =>
            ProviderCapabilities.TextChat |
            ProviderCapabilities.Streaming |
            ProviderCapabilities.ImageInput |
            ProviderCapabilities.ToolCalling |
            ProviderCapabilities.SystemPrompt;

        public override ProviderFieldHints FieldHints => new()
        {
            ShowApiKey = false,
            ApiKeyRequired = false,
            ShowBaseUrl = false
        };

        public override IReadOnlyList<ProviderAction> Actions => new[]
        {
            new ProviderAction
            {
                Id = "claude-login",
                Label = "Login with Claude.ai",
                Placement = ProviderActionPlacement.ApiKeyArea
            },
        };

        public override List<ModelDefinition> GetDefaultModels() => ModelCatalog.GetDefaults("Anthropic");

        public override async Task<AiResponse> SendChatAsync(IEnumerable<ChatMessage> messages, CancellationToken cancellationToken = default)
        {
            var sw = Stopwatch.StartNew();
            var sb = new StringBuilder();
            try
            {
                await foreach (var chunk in StreamChatAsync(messages, cancellationToken).ConfigureAwait(false))
                    sb.Append(chunk);

                return new AiResponse { Content = sb.ToString(), IsSuccess = true, Duration = sw.Elapsed };
            }
            catch (Exception ex)
            {
                return new AiResponse { IsSuccess = false, ErrorMessage = ex.Message, Duration = sw.Elapsed };
            }
        }

        public override async IAsyncEnumerable<string> StreamChatAsync(
            IEnumerable<ChatMessage> messages,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            if (!ClaudeAiSession.Instance.IsReady && ClaudeAiSession.PromptLogin != null)
                await ClaudeAiSession.PromptLogin().ConfigureAwait(false);

            if (!ClaudeAiSession.Instance.IsReady)
                throw new InvalidOperationException("Claude.ai is not configured. Login with Claude.ai first.");

            await foreach (var chunk in StreamViaSessionAsync(messages, cancellationToken).ConfigureAwait(false))
                yield return chunk;
        }

        public override Task<ProviderValidationResult> ValidateConfigurationAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(ClaudeAiSession.Instance.IsReady ? ProviderValidationResult.Ok() : ProviderValidationResult.Fail("Claude.ai is not configured. Login with Claude.ai first."));

        private async Task<string> GetOrgUuidAsync(CancellationToken ct)
        {
            if (_cachedOrgUuid != null) return _cachedOrgUuid;

            var json = await ClaudeAiSession.Instance.RequestAsync(
                "GET",
                "https://claude.ai/api/organizations",
                ct: ct).ConfigureAwait(false);

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0)
            {
                _cachedOrgUuid = root[0].GetProperty("uuid").GetString()
                    ?? throw new Exception("Organization UUID is null.");
                return _cachedOrgUuid;
            }

            throw new Exception("No organizations found on this claude.ai account.");
        }

        private async IAsyncEnumerable<string> StreamViaSessionAsync(
            IEnumerable<ChatMessage> messages,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var orgUuid = await GetOrgUuidAsync(cancellationToken).ConfigureAwait(false);
            var convUuid = Guid.NewGuid().ToString();

            var createBody = JsonSerializer.Serialize(new { uuid = convUuid, name = "" });
            await ClaudeAiSession.Instance.RequestAsync(
                "POST",
                $"https://claude.ai/api/organizations/{orgUuid}/chat_conversations",
                createBody,
                cancellationToken).ConfigureAwait(false);

            var sb = new StringBuilder();
            foreach (var msg in messages)
            {
                sb.Append(msg.Role switch
                {
                    "user" => $"\n\nHuman: {msg.Content}",
                    "assistant" => $"\n\nAssistant: {msg.Content}",
                    "system" => $"\n\nSystem: {msg.Content}",
                    _ => $"\n\n{msg.Content}"
                });
            }
            sb.Append("\n\nAssistant:");

            var body = JsonSerializer.Serialize(new
            {
                prompt = sb.ToString(),
                model = Config?.Model ?? "claude-sonnet-4-5",
                max_tokens_to_sample = Config?.MaxTokens > 0 ? Config.MaxTokens : 4096,
                timezone = TimeZoneInfo.Local.Id,
                attachments = Array.Empty<object>(),
                files = Array.Empty<object>()
            });

            var url = $"https://claude.ai/api/organizations/{orgUuid}/chat_conversations/{convUuid}/completion";
            await foreach (var chunk in ClaudeAiSession.Instance.StreamAsync(url, body, cancellationToken).ConfigureAwait(false))
                yield return chunk;
        }
    }
}
