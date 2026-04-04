using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Aire.Data;
using Aire.Domain.Providers;

namespace Aire.Providers;

/// <summary>
/// Minimal cross-platform Ollama provider used by Aire.Core and the Avalonia desktop shell.
/// It supports local model discovery and plain chat requests against the Ollama HTTP API.
/// </summary>
public sealed class PortableOllamaProvider : BaseAiProvider
{
    private const int MaxSupportedTimeoutMinutes = 35791;
    private readonly HttpClient _httpClient = new();
    private TimeSpan _requestTimeout = TimeSpan.FromMinutes(5);

    private static readonly JsonSerializerOptions SerializeOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly JsonSerializerOptions DeserializeOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public override string ProviderType => "Ollama";
    public override string DisplayName => "Ollama (Local)";

    protected override ProviderCapabilities GetBaseCapabilities() =>
        ProviderCapabilities.TextChat |
        ProviderCapabilities.Streaming |
        ProviderCapabilities.SystemPrompt;

    public override ProviderFieldHints FieldHints => new()
    {
        ApiKeyLabel = "API Key (optional)",
        ApiKeyRequired = false,
        ShowBaseUrl = true
    };

    public override void Initialize(ProviderConfig config)
    {
        base.Initialize(config);
        _requestTimeout = TimeSpan.FromMinutes(Math.Clamp(config.TimeoutMinutes, 1, MaxSupportedTimeoutMinutes));
        _httpClient.Timeout = _requestTimeout;
    }

    public override IReadOnlyList<ProviderAction> Actions => new[]
    {
        new ProviderAction
        {
            Id = "refresh-models",
            Label = "Refresh Models",
            Placement = ProviderActionPlacement.ModelArea,
        },
    };

    private string EffectiveBaseUrl =>
        string.IsNullOrWhiteSpace(Config.BaseUrl)
            ? "http://localhost:11434"
            : Config.BaseUrl!.TrimEnd('/');

    public override async Task<List<ModelDefinition>?> FetchLiveModelsAsync(
        string? apiKey,
        string? baseUrl,
        CancellationToken ct)
    {
        var url = string.IsNullOrWhiteSpace(baseUrl) ? "http://localhost:11434" : baseUrl.TrimEnd('/');

        // ── 1. Local Ollama (/api/tags) — determines if provider is reachable ──
        OllamaTagsResponse? localTags = null;
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, $"{url}/api/tags");
            if (!string.IsNullOrWhiteSpace(apiKey))
                req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
            var resp = await _httpClient.SendAsync(req, ct).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
                return null;
            var json = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            localTags = JsonSerializer.Deserialize<OllamaTagsResponse>(json, DeserializeOptions);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Ollama local fetch failed: {ex.GetType().Name}");
            return null;
        }

        // ── 2. Ollama web catalog (ollama.com/api/tags) — sizes for all models ──
        // Best-effort; failure is silent so offline users still see installed models.
        var webModels = new Dictionary<string, OllamaTagModel>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var webCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            webCts.CancelAfter(TimeSpan.FromSeconds(6));
            var webJson = await _httpClient
                .GetStringAsync("https://ollama.com/api/tags", webCts.Token)
                .ConfigureAwait(false);
            var webTags = JsonSerializer.Deserialize<OllamaTagsResponse>(webJson, DeserializeOptions);
            if (webTags?.Models != null)
                foreach (var m in webTags.Models)
                    webModels[NormalizeModelId(m.Name)] = m;
        }
        catch { /* offline or timeout — continue without web sizes */ }

        // ── 3. Merge: installed → web catalog → static defaults ─────────────
        var defaults  = ModelCatalog.GetDefaults("Ollama");
        var merged    = new List<ModelDefinition>();
        var seen      = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Installed models (local sizes are authoritative; fall back to web size)
        foreach (var m in localTags?.Models ?? [])
        {
            var id = NormalizeModelId(m.Name);
            if (!seen.Add(id)) continue;
            var webSize = webModels.TryGetValue(id, out var wm) ? wm.Size : 0;
            merged.Add(new ModelDefinition
            {
                Id          = id,
                DisplayName = id,
                SizeBytes   = m.Size > 0 ? m.Size : webSize,
                IsInstalled = true,
            });
        }

        // Web catalog models (not installed): use display name / capabilities from static catalog if available
        foreach (var (id, wm) in webModels)
        {
            if (!seen.Add(id)) continue;
            var cat = defaults.FirstOrDefault(d => string.Equals(d.Id, id, StringComparison.OrdinalIgnoreCase));
            merged.Add(new ModelDefinition
            {
                Id           = id,
                DisplayName  = cat?.DisplayName ?? id,
                SizeBytes    = wm.Size,
                Capabilities = cat?.Capabilities,
            });
        }

        // Static catalog entries missing from web response
        foreach (var m in defaults)
        {
            if (seen.Add(m.Id))
                merged.Add(m);
        }

        return merged;
    }

    public override async Task<AiResponse> SendChatAsync(
        IEnumerable<ChatMessage> messages,
        CancellationToken cancellationToken = default)
    {
        var startedAt = DateTime.UtcNow;

        try
        {
            var request = new OllamaChatRequest
            {
                Model = Config.Model,
                Stream = false,
                Messages = messages.Select(m => new OllamaChatMessage
                {
                    Role = string.IsNullOrWhiteSpace(m.Role) ? "user" : m.Role,
                    Content = m.Content ?? string.Empty
                }).ToList(),
                Options = new OllamaOptions
                {
                    Temperature = Config.Temperature,
                    NumPredict = Config.MaxTokens > 0 ? Config.MaxTokens : null
                }
            };

            var payload = JsonSerializer.Serialize(request, SerializeOptions);
            using var content = new StringContent(payload, Encoding.UTF8, "application/json");
            using var response = await _httpClient
                .PostAsync($"{EffectiveBaseUrl}/api/chat", content, cancellationToken)
                .ConfigureAwait(false);

            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return new AiResponse
                {
                    IsSuccess = false,
                    ErrorMessage = $"Ollama API error ({(int)response.StatusCode}): {body}",
                    Duration = DateTime.UtcNow - startedAt
                };
            }

            var parsed = JsonSerializer.Deserialize<OllamaChatResponse>(body, DeserializeOptions);
            return new AiResponse
            {
                Content = parsed?.Message?.Content ?? string.Empty,
                TokensUsed = parsed?.EvalCount ?? 0,
                Duration = DateTime.UtcNow - startedAt,
                IsSuccess = true
            };
        }
        catch (HttpRequestException)
        {
            return new AiResponse
            {
                IsSuccess = false,
                ErrorMessage = $"Network error contacting Ollama. Make sure Ollama is running at {EffectiveBaseUrl}",
                Duration = DateTime.UtcNow - startedAt
            };
        }
        catch (TaskCanceledException)
        {
            return new AiResponse
            {
                IsSuccess = false,
                ErrorMessage = $"Request timed out after {(int)_requestTimeout.TotalMinutes} minutes.",
                Duration = DateTime.UtcNow - startedAt
            };
        }
            catch
        {
            return new AiResponse
            {
                IsSuccess = false,
                ErrorMessage = "Ollama request failed.",
                Duration = DateTime.UtcNow - startedAt
            };
        }
    }

    public override async Task<ProviderValidationResult> ValidateConfigurationAsync(CancellationToken cancellationToken = default)
    {
        var models = await FetchLiveModelsAsync(Config.ApiKey, Config.BaseUrl, cancellationToken).ConfigureAwait(false);
        return models != null
            ? ProviderValidationResult.Ok()
            : ProviderValidationResult.Fail("Could not connect to Ollama or no models found.");
    }

    private static string NormalizeModelId(string id)
        => id.EndsWith(":latest", StringComparison.OrdinalIgnoreCase) ? id[..^7] : id;

    private sealed class OllamaTagsResponse
    {
        public List<OllamaTagModel> Models { get; set; } = [];
    }

    private sealed class OllamaTagModel
    {
        public string Name { get; set; } = string.Empty;
        public long Size { get; set; }
    }

    private sealed class OllamaChatRequest
    {
        public string Model { get; set; } = string.Empty;
        public bool Stream { get; set; }
        public List<OllamaChatMessage> Messages { get; set; } = [];
        public OllamaOptions? Options { get; set; }
    }

    private sealed class OllamaChatMessage
    {
        public string Role { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
    }

    private sealed class OllamaOptions
    {
        public double? Temperature { get; set; }
        public int? NumPredict { get; set; }
    }

    private sealed class OllamaChatResponse
    {
        public OllamaChatMessage? Message { get; set; }
        public int? EvalCount { get; set; }
    }
}
