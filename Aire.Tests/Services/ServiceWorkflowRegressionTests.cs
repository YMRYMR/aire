using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Aire.AppLayer.Abstractions;
using Aire.AppLayer.Api;
using Aire.AppLayer.Chat;
using Aire.AppLayer.Providers;
using Aire.AppLayer.Tools;
using Aire.Data;
using Aire.Domain.Providers;
using Aire.Domain.Tools;
using Aire.Providers;
using Aire.Services;
using Aire.Services.Policies;
using Aire.Services.Tools;
using Aire.Services.Workflows;
using Xunit;

namespace Aire.Tests.Services;

public class ServiceWorkflowRegressionTests
{
    private sealed class FakeOllamaManagementClient : IOllamaManagementClient
    {
        public Exception? InstallException { get; set; }

        public Exception? PullException { get; set; }

        public Exception? DeleteException { get; set; }

        public string? LastPulledModel { get; private set; }

        public string? LastDeletedModel { get; private set; }

        public string? LastBaseUrl { get; private set; }

        public Task InstallAsync(IProgress<int>? progress = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (InstallException != null)
            {
                throw InstallException;
            }
            progress?.Report(100);
            return Task.CompletedTask;
        }

        public Task PullModelAsync(string modelName, string? baseUrl = null, IProgress<OllamaService.OllamaPullProgress>? progress = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (PullException != null)
            {
                throw PullException;
            }
            LastPulledModel = modelName;
            LastBaseUrl = baseUrl;
            progress?.Report(new OllamaService.OllamaPullProgress
            {
                Status = "done",
                Total = 1L,
                Completed = 1L
            });
            return Task.CompletedTask;
        }

        public Task DeleteModelAsync(string modelName, string? baseUrl = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (DeleteException != null)
            {
                throw DeleteException;
            }
            LastDeletedModel = modelName;
            LastBaseUrl = baseUrl;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeCodexManagementClient : ICodexManagementClient
    {
        public Exception? InstallException { get; set; }

        public bool InstallCalled { get; private set; }

        public CodexCliStatus Status { get; set; } = new(false, null, false, "Codex CLI not found.");

        public Task<CodexCliStatus> GetStatusAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(Status);

        public Task InstallAsync(IProgress<string>? progress = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            InstallCalled = true;
            if (InstallException != null)
            {
                throw InstallException;
            }
            progress?.Report("Installing Codex CLIÃ¢â¬Â¦");
            progress?.Report("Codex CLI installed.");
            return Task.CompletedTask;
        }
    }

    private sealed class FakeProviderRuntimeGateway : IProviderRuntimeGateway
    {
        public IAiProvider? ProviderToReturn { get; set; }

        public ProviderSmokeTestResult SmokeTestResult { get; set; } = new ProviderSmokeTestResult(Success: true);

        public IAiProvider? LastSmokeTestProvider { get; private set; }

        public IAiProvider? BuildProvider(ProviderRuntimeRequest request)
        {
            return ProviderToReturn;
        }

        public Task<ProviderSmokeTestResult> RunSmokeTestAsync(IAiProvider provider, CancellationToken cancellationToken)
        {
            LastSmokeTestProvider = provider;
            return Task.FromResult(SmokeTestResult);
        }
    }

    private sealed class LocalHttpServer : IDisposable
    {
        private readonly HttpListener _listener = new HttpListener();

        private readonly Func<HttpListenerRequest, LocalHttpResponse> _handler;

        private readonly Task _serveTask;

        public string Url { get; }

        public LocalHttpServer(Func<HttpListenerRequest, LocalHttpResponse> handler)
        {
            _handler = handler;
            string uriPrefix = (Url = $"http://127.0.0.1:{GetFreePort()}/");
            _listener.Prefixes.Add(uriPrefix);
            _listener.Start();
            _serveTask = Task.Run((Func<Task?>)ServeAsync);
        }

        private async Task ServeAsync()
        {
            try
            {
                while (_listener.IsListening)
                {
                    HttpListenerContext context = await _listener.GetContextAsync();
                    LocalHttpResponse response = _handler(context.Request);
                    context.Response.StatusCode = response.StatusCode;
                    context.Response.ContentType = response.ContentType;
                    byte[] bytes = Encoding.UTF8.GetBytes(response.Body);
                    context.Response.ContentLength64 = bytes.Length;
                    await context.Response.OutputStream.WriteAsync(bytes);
                    context.Response.OutputStream.Close();
                }
            }
            catch (HttpListenerException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
        }

        public void Dispose()
        {
            _listener.Stop();
            _listener.Close();
            try
            {
                _serveTask.GetAwaiter().GetResult();
            }
            catch
            {
            }
        }

        private static int GetFreePort()
        {
            TcpListener tcpListener = new TcpListener(IPAddress.Loopback, 0);
            tcpListener.Start();
            int port = ((IPEndPoint)tcpListener.LocalEndpoint).Port;
            tcpListener.Stop();
            return port;
        }
    }

    private sealed record LocalHttpResponse(int StatusCode, string ContentType, string Body);

    private sealed class StubProviderWithCapabilities(ProviderCapabilities capabilities) : IAiProvider
    {
        public string ProviderType => "Stub";

        public string DisplayName => "Stub";

        public ProviderCapabilities Capabilities => capabilities;

        public ToolCallMode ToolCallMode => ToolCallMode.TextBased;

        public ToolOutputFormat ToolOutputFormat => ToolOutputFormat.AireText;

        public bool Has(ProviderCapabilities cap)
        {
            return (Capabilities & cap) == cap;
        }

        public void Initialize(ProviderConfig config)
        {
        }

        public void PrepareForCapabilityTesting() { }
        public void SetToolsEnabled(bool enabled) { }
        public void SetEnabledToolCategories(IEnumerable<string>? categories) { }

        public Task<AiResponse> SendChatAsync(IEnumerable<ChatMessage> messages, CancellationToken cancellationToken = default(CancellationToken))
        {
            return Task.FromResult(new AiResponse
            {
                IsSuccess = true,
                Content = "ok"
            });
        }

        public async IAsyncEnumerable<string> StreamChatAsync(IEnumerable<ChatMessage> messages, [EnumeratorCancellation] CancellationToken cancellationToken = default(CancellationToken))
        {
            yield break;
        }

        public Task<ProviderValidationResult> ValidateConfigurationAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            return Task.FromResult(ProviderValidationResult.Ok());
        }

        public Task<TokenUsage?> GetTokenUsageAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            return Task.FromResult<TokenUsage>(null);
        }
    }

    private sealed class TestMetadataWithLiveModels(string providerType, List<ModelDefinition> defaultModels, List<ModelDefinition>? liveModels) : IProviderMetadata
    {
        public string ProviderType => providerType;

        public string DisplayName => providerType;

        public ProviderFieldHints FieldHints { get; } = new ProviderFieldHints
        {
            ShowApiKey = true,
            ApiKeyRequired = true,
            ShowBaseUrl = true
        };

        public IReadOnlyList<ProviderAction> Actions { get; } = Array.Empty<ProviderAction>();

        public List<ModelDefinition> GetDefaultModels()
        {
            return defaultModels;
        }

        public Task<List<ModelDefinition>?> FetchLiveModelsAsync(string? apiKey, string? baseUrl, CancellationToken ct)
        {
            return Task.FromResult(liveModels);
        }
    }

    private sealed class DelegatingMetadata(string providerType, List<ModelDefinition> defaultModels, Func<string?, string?, CancellationToken, Task<List<ModelDefinition>?>> fetchLiveModelsAsync) : IProviderMetadata
    {
        public string ProviderType => providerType;

        public string DisplayName => providerType;

        public ProviderFieldHints FieldHints { get; } = new ProviderFieldHints
        {
            ShowApiKey = true,
            ApiKeyRequired = true,
            ShowBaseUrl = true
        };

        public IReadOnlyList<ProviderAction> Actions { get; } = Array.Empty<ProviderAction>();

        public List<ModelDefinition> GetDefaultModels()
        {
            return defaultModels;
        }

        public Task<List<ModelDefinition>?> FetchLiveModelsAsync(string? apiKey, string? baseUrl, CancellationToken ct)
        {
            return fetchLiveModelsAsync(apiKey, baseUrl, ct);
        }
    }

    private sealed class StubSmokeTestProvider(bool success, string? errorMessage = null) : IAiProvider
    {
        public string ProviderType => "Stub";

        public string DisplayName => "Stub";

        public ProviderCapabilities Capabilities => ProviderCapabilities.None;

        public ToolCallMode ToolCallMode => ToolCallMode.TextBased;

        public ToolOutputFormat ToolOutputFormat => ToolOutputFormat.AireText;

        public bool Has(ProviderCapabilities cap)
        {
            return false;
        }

        public void Initialize(ProviderConfig config)
        {
        }

        public void PrepareForCapabilityTesting() { }
        public void SetToolsEnabled(bool enabled) { }
        public void SetEnabledToolCategories(IEnumerable<string>? categories) { }

        public Task<AiResponse> SendChatAsync(IEnumerable<ChatMessage> messages, CancellationToken cancellationToken = default(CancellationToken))
        {
            return Task.FromResult(new AiResponse
            {
                IsSuccess = success,
                Content = (success ? "ok" : string.Empty),
                ErrorMessage = errorMessage
            });
        }

        public async IAsyncEnumerable<string> StreamChatAsync(IEnumerable<ChatMessage> messages, [EnumeratorCancellation] CancellationToken cancellationToken = default(CancellationToken))
        {
            yield break;
        }

        public Task<ProviderValidationResult> ValidateConfigurationAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            return Task.FromResult(success ? ProviderValidationResult.Ok() : ProviderValidationResult.Fail(errorMessage ?? "Failed"));
        }

        public Task<TokenUsage?> GetTokenUsageAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            return Task.FromResult<TokenUsage>(null);
        }
    }

    private sealed class InMemoryProviderRepository : IProviderRepository
    {
        private readonly List<Provider> _providers;

        public InMemoryProviderRepository(params Provider[] providers)
        {
            _providers = providers.ToList();
        }

        public Task<List<Provider>> GetProvidersAsync()
        {
            return Task.FromResult(_providers.Select(Clone).ToList());
        }

        public Task UpdateProviderAsync(Provider provider)
        {
            int num = _providers.FindIndex((Provider p) => p.Id == provider.Id);
            if (num >= 0)
            {
                _providers[num] = Clone(provider);
            }
            return Task.CompletedTask;
        }

        public Task SaveProviderOrderAsync(IEnumerable<Provider> orderedProviders)
        {
            _providers.Clear();
            _providers.AddRange(orderedProviders.Select(Clone));
            return Task.CompletedTask;
        }

        public Task<int> InsertProviderAsync(Provider provider)
        {
            Provider provider2 = Clone(provider);
            if (provider2.Id == 0)
            {
                provider2.Id = ((_providers.Count == 0) ? 1 : (_providers.Max((Provider p) => p.Id) + 1));
            }
            _providers.Add(provider2);
            return Task.FromResult(provider2.Id);
        }

        public Task DeleteProviderAsync(int id)
        {
            _providers.RemoveAll((Provider p) => p.Id == id);
            return Task.CompletedTask;
        }

        private static Provider Clone(Provider provider)
        {
            return new Provider
            {
                Id = provider.Id,
                Name = provider.Name,
                Type = provider.Type,
                ApiKey = provider.ApiKey,
                BaseUrl = provider.BaseUrl,
                Model = provider.Model,
                IsEnabled = provider.IsEnabled,
                Color = provider.Color,
                SortOrder = provider.SortOrder,
                TimeoutMinutes = provider.TimeoutMinutes
            };
        }
    }

    private sealed class InMemorySettingsRepository : ISettingsRepository
    {
        private readonly Dictionary<string, string> _values = new Dictionary<string, string>(StringComparer.Ordinal);

        public Task<string?> GetSettingAsync(string key)
        {
            string value;
            return Task.FromResult(_values.TryGetValue(key, out value) ? value : null);
        }

        public Task SetSettingAsync(string key, string value)
        {
            _values[key] = value;
            return Task.CompletedTask;
        }

        public Task LogFileAccessAsync(string operation, string path, bool allowed)
        {
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task WebFetchService_FetchesHtmlAndExtractsReadableText()
    {
        using LocalHttpServer host = new LocalHttpServer((HttpListenerRequest _) => new LocalHttpResponse(200, "text/html", "<html>\r\n  <head>\r\n    <title>Aire Docs</title>\r\n    <style>.hidden{display:none}</style>\r\n    <script>console.log('ignore');</script>\r\n  </head>\r\n  <body>\r\n    <main>\r\n      <h1>Welcome</h1>\r\n      <p>Aire helps you work with AI safely.</p>\r\n    </main>\r\n  </body>\r\n</html>"));
        using WebFetchService service = new WebFetchService();
        WebFetchResult result = await service.FetchAsync(host.Url);
        Assert.Equal("Aire Docs", result.Title);
        Assert.Contains("Welcome", result.Text, StringComparison.Ordinal);
        Assert.Contains("Aire helps you work with AI safely.", result.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("console.log", result.Text, StringComparison.Ordinal);
        Assert.False(result.Truncated);
        Assert.Equal(host.Url, result.Url);
    }

    [Fact]
    public async Task WebFetchService_FetchesRssAndFormatsEntries()
    {
        using LocalHttpServer host = new LocalHttpServer((HttpListenerRequest _) => new LocalHttpResponse(200, "application/rss+xml", "<?xml version=\"1.0\"?>\r\n<rss version=\"2.0\">\r\n  <channel>\r\n    <title>Aire Feed</title>\r\n    <item>\r\n      <title>First post</title>\r\n      <link>https://example.com/first</link>\r\n      <description><![CDATA[<p>Important update</p>]]></description>\r\n    </item>\r\n    <item>\r\n      <title>Second post</title>\r\n      <link>https://example.com/second</link>\r\n      <description>Another change</description>\r\n    </item>\r\n  </channel>\r\n</rss>"));
        using WebFetchService service = new WebFetchService();
        WebFetchResult result = await service.FetchAsync(host.Url);
        Assert.Equal("Aire Feed", result.Title);
        Assert.Contains("Title: First post", result.Text, StringComparison.Ordinal);
        Assert.Contains("Link:  https://example.com/first", result.Text, StringComparison.Ordinal);
        Assert.Contains("Important update", result.Text, StringComparison.Ordinal);
        Assert.Contains("Title: Second post", result.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WebFetchService_ReturnsHelpfulHttpErrorForBlockedSites()
    {
        using LocalHttpServer host = new LocalHttpServer((HttpListenerRequest _) => new LocalHttpResponse(403, "text/plain", "forbidden"));
        using WebFetchService service = new WebFetchService();
        WebFetchResult result = await service.FetchAsync(host.Url);
        Assert.Contains("HTTP 403", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Contains("blocking automated access", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/rss", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Equal(host.Url, result.Url);
    }

    [Fact]
    public async Task InputToolService_ReturnsUnknownToolMessage_ForUnsupportedRequest()
    {
        InputToolService service = new InputToolService();
        Assert.Contains("Unknown input tool", (await service.ExecuteAsync(CreateRequest("unknown_input_tool", "{}"))).TextResult, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task InputToolService_KeyCombo_RequiresAtLeastOneKey()
    {
        InputToolService service = new InputToolService();
        Assert.Contains("no keys specified", (await service.ExecuteAsync(CreateRequest("key_combo", "{\"keys\":[]}"))).TextResult, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ToolAutoAcceptPolicyService_UsesAliases_And_MouseKeyboardFlags()
    {
        ToolAutoAcceptPolicyService service = new ToolAutoAcceptPolicyService(() => Task.FromResult<string>(null));
        Assert.True(await service.IsAutoAcceptedAsync("write_file", "{\"Enabled\":true,\"AllowedTools\":[\"write_to_file\",\"list_files\"],\"AllowMouseTools\":true,\"AllowKeyboardTools\":true}"));
        Assert.True(await service.IsAutoAcceptedAsync("list_directory", "{\"Enabled\":true,\"AllowedTools\":[\"write_to_file\",\"list_files\"],\"AllowMouseTools\":true,\"AllowKeyboardTools\":true}"));
        Assert.True(await service.IsAutoAcceptedAsync("click", "{\"Enabled\":true,\"AllowedTools\":[\"write_to_file\",\"list_files\"],\"AllowMouseTools\":true,\"AllowKeyboardTools\":true}"));
        Assert.True(await service.IsAutoAcceptedAsync("type_text", "{\"Enabled\":true,\"AllowedTools\":[\"write_to_file\",\"list_files\"],\"AllowMouseTools\":true,\"AllowKeyboardTools\":true}"));
        Assert.False(await service.IsAutoAcceptedAsync("delete_file", "{\"Enabled\":true,\"AllowedTools\":[\"write_to_file\",\"list_files\"],\"AllowMouseTools\":true,\"AllowKeyboardTools\":true}"));
    }

    [Fact]
    public void ToolFollowUpWorkflowService_ParsesTodoTasks_AndFollowUpQuestions()
    {
        ToolFollowUpWorkflowService toolFollowUpWorkflowService = new ToolFollowUpWorkflowService();
        using JsonDocument jsonDocument = JsonDocument.Parse("{\"tasks\":\"[{\\\"id\\\":\\\"1\\\",\\\"description\\\":\\\"Draft reply\\\",\\\"status\\\":\\\"completed\\\"},{\\\"id\\\":\\\"2\\\",\\\"description\\\":\\\"Send it\\\",\\\"status\\\":\\\"pending\\\"}]\"}");
        IReadOnlyList<ToolFollowUpWorkflowService.TodoTask> readOnlyList = toolFollowUpWorkflowService.ParseTodoTasks(jsonDocument.RootElement);
        Assert.Equal(2, readOnlyList.Count);
        Assert.Equal("Draft reply", readOnlyList[0].Description);
        Assert.Equal("completed", readOnlyList[0].Status);
        Assert.Equal("Todo list updated: 2 task(s), 1 completed.", toolFollowUpWorkflowService.BuildTodoUpdateStatus(readOnlyList));
        using JsonDocument jsonDocument2 = JsonDocument.Parse("{\"question\":\"Which draft should I send?\",\"options\":\"formal, friendly\"}");
        ToolFollowUpWorkflowService.FollowUpQuestionRequest followUpQuestionRequest = toolFollowUpWorkflowService.ParseFollowUpQuestion(jsonDocument2.RootElement);
        Assert.NotNull(followUpQuestionRequest);
        Assert.Equal("Which draft should I send?", followUpQuestionRequest.Question);
        Assert.Equal(new string[] { "formal", "friendly" }, followUpQuestionRequest.Options);
        Assert.Equal("C:\\temp\\note.txt", toolFollowUpWorkflowService.GetPathFromRequest(CreateRequest("read_file", "{\"path\":\"C:\\\\temp\\\\note.txt\"}")));
        Assert.Contains("<tool_call>", toolFollowUpWorkflowService.BuildAssistantToolCallContent("Thinking", "{\"tool\":\"read_file\"}"), StringComparison.Ordinal);
        Assert.Contains("[File system result", toolFollowUpWorkflowService.BuildToolResultHistoryContent("read_file", "ok"), StringComparison.Ordinal);
    }

    [Fact]
    public void ProviderActivationWorkflowService_BuildsConversationPlans()
    {
        ProviderActivationWorkflowService providerActivationWorkflowService = new ProviderActivationWorkflowService();
        ProviderActivationWorkflowService.ProviderActivationPlan providerActivationPlan = providerActivationWorkflowService.BuildPlan(1, 2, 99, null, "Ollama", showSwitchedMessage: true);
        Assert.True(providerActivationPlan.ProviderChanged);
        Assert.True(providerActivationPlan.ShouldAnnounceSwitch);
        Assert.Equal(ProviderActivationWorkflowService.ConversationActionKind.KeepCurrentConversation, providerActivationPlan.ConversationAction);
        Assert.Equal(99, providerActivationPlan.ConversationIdToLoad);
        ProviderActivationWorkflowService.ProviderActivationPlan providerActivationPlan2 = providerActivationWorkflowService.BuildPlan(2, 2, null, new Aire.Data.Conversation
        {
            Id = 7,
            ProviderId = 2,
            Title = "Existing"
        }, "Ollama", showSwitchedMessage: true);
        Assert.False(providerActivationPlan2.ProviderChanged);
        Assert.Equal(ProviderActivationWorkflowService.ConversationActionKind.LoadExistingConversation, providerActivationPlan2.ConversationAction);
        Assert.Equal(7, providerActivationPlan2.ConversationIdToLoad);
        ProviderActivationWorkflowService.ProviderActivationPlan providerActivationPlan3 = providerActivationWorkflowService.BuildPlan(null, 3, null, null, "OpenAI", showSwitchedMessage: true);
        Assert.Equal(ProviderActivationWorkflowService.ConversationActionKind.CreateNewConversation, providerActivationPlan3.ConversationAction);
        Assert.Equal("Chat", providerActivationPlan3.NewConversationTitle);
        Assert.Contains("OpenAI", providerActivationPlan3.NewConversationMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void ChatSubmissionWorkflowService_PreparesAttachmentsAndHistory()
    {
        ChatSubmissionWorkflowService chatSubmissionWorkflowService = new ChatSubmissionWorkflowService();
        string text = Path.Combine(Path.GetTempPath(), $"aire-submission-{Guid.NewGuid():N}.txt");
        File.WriteAllText(text, "console.log('hello');");
        try
        {
            ChatSubmissionWorkflowService.PreparedSubmission preparedSubmission = chatSubmissionWorkflowService.PrepareSubmission("Review this file", null, text, new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".txt" }, 0);
            Assert.Contains("Attached:", preparedSubmission.DisplayContent, StringComparison.Ordinal);
            Assert.Contains("console.log('hello');", preparedSubmission.DisplayContent, StringComparison.Ordinal);
            Assert.Equal("Review this file", preparedSubmission.SuggestedConversationTitle);
            Assert.Null(preparedSubmission.HistoryImagePath);
            ChatSubmissionWorkflowService.PreparedSubmission preparedSubmission2 = chatSubmissionWorkflowService.PrepareSubmission("See screenshot", null, "C:\\temp\\capture.png", new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".txt" }, 2);
            Assert.Equal("C:\\temp\\capture.png", preparedSubmission2.HistoryImagePath);
            Assert.Null(preparedSubmission2.SuggestedConversationTitle);
            (int, string) tuple = chatSubmissionWorkflowService.UpdateInputHistory(new List<string> { "old" }, "new");
            Assert.Equal(-1, tuple.Item1);
            Assert.Equal(string.Empty, tuple.Item2);
            ChatMessage chatMessage = chatSubmissionWorkflowService.BuildProviderHistoryMessage(
                "hello",
                "C:\\temp\\capture.png",
                null);
            Assert.Equal("user", chatMessage.Role);
            Assert.Equal("hello", chatMessage.Content);
            Assert.Equal("C:\\temp\\capture.png", chatMessage.ImagePath);
        }
        finally
        {
            try
            {
                File.Delete(text);
            }
            catch
            {
            }
        }
    }

    [Fact]
    public void ProviderPresentationWorkflowService_TrimsHistory_AndBuildsCapabilityText()
    {
        ProviderPresentationWorkflowService providerPresentationWorkflowService = new ProviderPresentationWorkflowService();
        List<ChatMessage> history = new List<ChatMessage>
        {
            new ChatMessage
            {
                Role = "system",
                Content = "rules"
            },
            new ChatMessage
            {
                Role = "user",
                Content = "1"
            },
            new ChatMessage
            {
                Role = "assistant",
                Content = "2"
            },
            new ChatMessage
            {
                Role = "user",
                Content = "3"
            }
        };
        List<ChatMessage> list = providerPresentationWorkflowService.TrimConversation(history, 2);
        Assert.Equal(4, list.Count);
        Assert.Equal("system", list[0].Role);
        Assert.Contains(list, message => message.Role == "user");
        Assert.Contains(list, message => message.Role == "assistant");
        Provider[] providers = new Provider[2]
        {
            new Provider
            {
                Id = 1,
                IsEnabled = true,
                Type = "OpenAI",
                Model = "gpt-4.1",
                Name = "OpenAI Main"
            },
            new Provider
            {
                Id = 2,
                IsEnabled = true,
                Type = "Ollama",
                Model = "llama3.2",
                Name = "Local Ollama"
            }
        };
        string actualString = providerPresentationWorkflowService.BuildModelListSection(providers, (int id) => id == 2);
        Assert.Contains("model_name=\"gpt-4.1\"", actualString, StringComparison.Ordinal);
        Assert.Contains("provider=Local Ollama", actualString, StringComparison.Ordinal);
        Assert.Contains("UNAVAILABLE", actualString, StringComparison.Ordinal);
        string actualString2 = providerPresentationWorkflowService.BuildCapabilityTooltip(ProviderCapabilities.Streaming | ProviderCapabilities.ImageInput | ProviderCapabilities.ToolCalling);
        Assert.Contains("images", actualString2, StringComparison.Ordinal);
        Assert.Contains("tool calling", actualString2, StringComparison.Ordinal);
        Assert.Contains("streaming", actualString2, StringComparison.Ordinal);
    }

    [Fact]
    public void ChatResponseWorkflowService_NormalizesPreviewAndCompletionResult()
    {
        ChatResponseWorkflowService chatResponseWorkflowService = new ChatResponseWorkflowService();
        Assert.Equal("(empty response)", chatResponseWorkflowService.NormalizeFinalText(string.Empty));
        Assert.Equal("hello", chatResponseWorkflowService.NormalizeFinalText("hello"));
        Assert.EndsWith("…", chatResponseWorkflowService.BuildTrayPreview(new string('a', 90), 10), StringComparison.Ordinal);
        ToolCallRequest request = CreateRequest("attempt_completion", "{\"result\":\"Task finished\"}");
        Assert.Equal("Task finished", chatResponseWorkflowService.ExtractCompletionResult(request));
    }

    [Fact]
    public async Task ChatTurnApplicationService_HandleSuccessTextAsync_PreservesAssistantImageOnlyResponses()
    {
        string localAppData = Path.Combine(Path.GetTempPath(), "aire-chat-image-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(localAppData);
        string oldLocalAppData = Environment.GetEnvironmentVariable("LOCALAPPDATA");
        Environment.SetEnvironmentVariable("LOCALAPPDATA", localAppData);
        try
        {
            using DatabaseService db = new DatabaseService();
            await db.InitializeAsync();
            int conversationId = await db.CreateConversationAsync(1, "Image Chat");
            var session = new ChatSessionApplicationService(db, db);
            var toolWorkflow = new ToolExecutionWorkflowService(new ToolExecutionService(new FileSystemService(), new CommandExecutionService()), db, db);
            var service = new ChatTurnApplicationService(session, toolWorkflow);

            var result = await service.HandleSuccessTextAsync("![chart](https://example.com/chart.png)", conversationId, isWindowVisible: true);

            Assert.Equal(string.Empty, result.FinalText);
            Assert.Single(result.ImageReferences);
            Assert.Equal("https://example.com/chart.png", result.ImageReferences[0]);
            Assert.Contains(await db.GetMessagesAsync(conversationId), message => message.Role == "assistant" && message.ImagePath == "https://example.com/chart.png");
        }
        finally
        {
            Environment.SetEnvironmentVariable("LOCALAPPDATA", oldLocalAppData);
            try
            {
                Directory.Delete(localAppData, recursive: true);
            }
            catch
            {
            }
        }
    }

    [Fact]
    public async Task ToolExecutionWorkflowService_PersistsApprovalAndDenialEffects()
    {
        string localAppData = Path.Combine(Path.GetTempPath(), "aire-tool-exec-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(localAppData);
        string oldLocalAppData = Environment.GetEnvironmentVariable("LOCALAPPDATA");
        Environment.SetEnvironmentVariable("LOCALAPPDATA", localAppData);
        try
        {
            using DatabaseService db = new DatabaseService();
            await db.InitializeAsync();
            int conversationId = await db.CreateConversationAsync(1, "Tool test");
            ToolExecutionService toolService = new ToolExecutionService(new FileSystemService(), new CommandExecutionService());
            ToolExecutionWorkflowService workflow = new ToolExecutionWorkflowService(toolService, db, db);
            ToolCallRequest deniedRequest = CreateRequest("read_file", "{\"path\":\"C:\\\\temp\\\\missing.txt\"}");
            deniedRequest.Description = "Read file: C:\\temp\\missing.txt";
            ToolExecutionWorkflowService.ExecutionOutcome denied = await workflow.ExecuteAsync(deniedRequest, approved: false, conversationId);
            Assert.False(denied.Approved);
            Assert.Equal("[Operation denied by user]", denied.ToolResult);
            Assert.Equal("✗ Denied", denied.ToolCallStatus);
            Assert.Contains("[File system result", denied.HistoryContent, StringComparison.Ordinal);
            Assert.True((await db.GetMessagesAsync(conversationId)).Any(m => m.Role == "tool" && m.Content.Contains("Denied")), "Expected denied tool message in database");
            ToolCallRequest approvedRequest = CreateRequest("read_file", "{\"path\":\"C:\\\\temp\\\\still-missing.txt\"}");
            approvedRequest.Description = "Read file: C:\\temp\\still-missing.txt";
            ToolExecutionWorkflowService.ExecutionOutcome approved = await workflow.ExecuteAsync(approvedRequest, approved: true, conversationId);
            Assert.True(approved.Approved);
            Assert.NotNull(approved.ExecutionResult);
            Assert.Contains("still-missing.txt", approved.ToolResult, StringComparison.OrdinalIgnoreCase);
            Assert.True(approved.ToolCallStatus.Contains("✓") || approved.ToolCallStatus.Contains("succeeded"), "Expected success indicator in status");
        }
        finally
        {
            Environment.SetEnvironmentVariable("LOCALAPPDATA", oldLocalAppData);
            try
            {
                Directory.Delete(localAppData, recursive: true);
            }
            catch
            {
            }
        }
    }

    [Fact]
    public async Task ToolApprovalExecutionApplicationService_CompletesApprovedAndDeniedRequests()
    {
        string localAppData = Path.Combine(Path.GetTempPath(), "aire-tool-approval-exec-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(localAppData);
        string oldLocalAppData = Environment.GetEnvironmentVariable("LOCALAPPDATA");
        Environment.SetEnvironmentVariable("LOCALAPPDATA", localAppData);
        try
        {
            using DatabaseService db = new DatabaseService();
            await db.InitializeAsync();
            int conversationId = await db.CreateConversationAsync(1, "Approval Test");
            ToolExecutionService toolService = new ToolExecutionService(new FileSystemService(), new CommandExecutionService());
            ToolApprovalExecutionApplicationService service = new ToolApprovalExecutionApplicationService(toolExecutionWorkflow: new ToolExecutionWorkflowService(toolService, db, db), promptService: new ToolApprovalPromptApplicationService());
            ToolCallRequest deniedRequest = CreateRequest("read_file", "{\"path\":\"C:\\\\temp\\\\missing.txt\"}");
            deniedRequest.Description = "Read file: C:\\temp\\missing.txt";
            ToolApprovalExecutionApplicationService.ApprovalExecutionResult denied = await service.CompleteAsync(deniedRequest, approved: false, conversationId);
            Assert.Equal("denied", denied.Status);
            Assert.Equal("✗ Denied", denied.ToolCallStatus);
            Assert.Equal("Tool execution was denied.", denied.TextResult);
            Assert.False(denied.ExecutionOutcome.Approved);
            ToolCallRequest approvedRequest = CreateRequest("read_file", "{\"path\":\"C:\\\\temp\\\\still-missing.txt\"}");
            approvedRequest.Description = "Read file: C:\\temp\\still-missing.txt";
            ToolApprovalExecutionApplicationService.ApprovalExecutionResult approved = await service.CompleteAsync(approvedRequest, approved: true, conversationId);
            Assert.Equal("completed", approved.Status);
            Assert.StartsWith("✓ Read file:", approved.ToolCallStatus, StringComparison.Ordinal);
            Assert.True(approved.ExecutionOutcome.Approved);
            Assert.Contains("still-missing.txt", approved.TextResult, StringComparison.OrdinalIgnoreCase);
            List<Message> messages = await db.GetMessagesAsync(conversationId);
            Assert.Contains((IEnumerable<Message>)messages, (Predicate<Message>)((Message m) => m.Role == "tool" && m.Content == "✗ Denied"));
            Assert.Contains((IEnumerable<Message>)messages, (Predicate<Message>)((Message m) => m.Role == "tool" && m.Content.StartsWith("✓ Read file:", StringComparison.Ordinal)));
        }
        finally
        {
            Environment.SetEnvironmentVariable("LOCALAPPDATA", oldLocalAppData);
            try
            {
                Directory.Delete(localAppData, recursive: true);
            }
            catch
            {
            }
        }
    }

    [Fact]
    public async Task ToolControlSessionApplicationService_TracksSessionLifecycle()
    {
        ToolApprovalApplicationService approvalService = new ToolApprovalApplicationService(new ToolAutoAcceptPolicyService(() => Task.FromResult<string>(null)));
        ToolControlSessionApplicationService service = new ToolControlSessionApplicationService(approvalService);
        DateTime now = new DateTime(2026, 3, 28, 12, 0, 0, DateTimeKind.Local);
        service.ApplyToolRequest(CreateRequest("begin_mouse_session", "{\"duration_minutes\":5}"), now);
        ToolControlSessionApplicationService.SessionBannerPlan activeBanner = service.BuildBannerPlan(now.AddMinutes(1.0));
        Assert.True(activeBanner.IsVisible);
        Assert.True(activeBanner.SessionActive);
        Assert.Contains("Mouse session active", activeBanner.BannerText, StringComparison.Ordinal);
        Assert.True((await service.DetermineAutoApproveAsync("click", now.AddMinutes(2.0))).AutoApprove);
        ToolControlSessionApplicationService.SessionBannerPlan expiredBanner = service.BuildBannerPlan(now.AddMinutes(6.0));
        Assert.False(expiredBanner.IsVisible);
        Assert.False(expiredBanner.SessionActive);
        service.ApplyToolRequest(CreateRequest("begin_keyboard_session", "{\"duration_minutes\":10}"), now);
        service.Stop();
        ToolControlSessionApplicationService.SessionBannerPlan stoppedBanner = service.BuildBannerPlan(now.AddMinutes(1.0));
        Assert.False(stoppedBanner.IsVisible);
        Assert.False(stoppedBanner.SessionActive);
    }

    [Fact]
    public async Task ChatSessionApplicationService_PersistsSelectedProvider_AndConversationMessages()
    {
        string localAppData = Path.Combine(Path.GetTempPath(), "aire-chat-session-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(localAppData);
        string oldLocalAppData = Environment.GetEnvironmentVariable("LOCALAPPDATA");
        Environment.SetEnvironmentVariable("LOCALAPPDATA", localAppData);
        try
        {
            using DatabaseService db = new DatabaseService();
            await db.InitializeAsync();
            ChatSessionApplicationService service = new ChatSessionApplicationService(db, db);
            await service.SaveSelectedProviderAsync(5);
            int? expected = 5;
            Assert.Equal(expected, await service.GetSelectedProviderIdAsync());
            int conversationId = await db.CreateConversationAsync(1, "Untitled");
            await service.PersistUserMessageAsync(conversationId, "hello", null, null, "Greeting");
            await service.PersistAssistantMessageAsync(conversationId, "hi there");
            await service.PersistAssistantMessageAsync(conversationId, "diagram", "https://example.com/diagram.png");
            await service.PersistToolStatusAsync(conversationId, "Ã¢Å“ Read file");
            await service.UpdateConversationProviderAsync(conversationId, 2);
            Aire.Data.Conversation conversation = await db.GetConversationAsync(conversationId);
            Assert.NotNull(conversation);
            Assert.Equal(2, conversation.ProviderId);
            Assert.Equal("Greeting", conversation.Title);
            List<Message> messages = await db.GetMessagesAsync(conversationId);
            Assert.Contains((IEnumerable<Message>)messages, (Predicate<Message>)((Message m) => m.Role == "user" && m.Content == "hello"));
            Assert.Contains((IEnumerable<Message>)messages, (Predicate<Message>)((Message m) => m.Role == "assistant" && m.Content == "hi there"));
            Assert.Contains((IEnumerable<Message>)messages, (Predicate<Message>)((Message m) => m.Role == "assistant" && m.ImagePath == "https://example.com/diagram.png"));
            Assert.Contains((IEnumerable<Message>)messages, (Predicate<Message>)((Message m) => m.Role == "tool" && m.Content == "Ã¢Å“ Read file"));
            Aire.Data.Conversation latest = await service.GetLatestConversationAsync(2);
            Assert.NotNull(latest);
            Assert.Equal(conversationId, latest.Id);
        }
        finally
        {
            Environment.SetEnvironmentVariable("LOCALAPPDATA", oldLocalAppData);
            try
            {
                Directory.Delete(localAppData, recursive: true);
            }
            catch
            {
            }
        }
    }

    [Fact]
    public async Task ConversationApplicationService_HandlesConversationLifecycle()
    {
        string localAppData = Path.Combine(Path.GetTempPath(), "aire-conversation-app-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(localAppData);
        string oldLocalAppData = Environment.GetEnvironmentVariable("LOCALAPPDATA");
        Environment.SetEnvironmentVariable("LOCALAPPDATA", localAppData);
        try
        {
            using DatabaseService db = new DatabaseService();
            await db.InitializeAsync();
            ConversationApplicationService service = new ConversationApplicationService(db);
            int firstId = await service.CreateConversationAsync(1, "First");
            await db.SaveMessageAsync(firstId, "user", "hello");
            await db.SaveMessageAsync(firstId, "assistant", "hi");
            Assert.Equal(2, (await service.GetMessagesAsync(firstId)).Count);
            await service.RenameConversationAsync(firstId, "Renamed");
            Assert.Contains((IEnumerable<ConversationSummary>)(await service.ListConversationsAsync("Rename")), (Predicate<ConversationSummary>)((ConversationSummary c) => c.Id == firstId && c.Title == "Renamed"));
            int secondId = await service.CreateConversationAsync(2, "Second");
            await db.SaveMessageAsync(secondId, "user", "another");
            await service.DeleteConversationAsync(firstId);
            List<ConversationSummary> afterDelete = await service.ListConversationsAsync();
            Assert.DoesNotContain((IEnumerable<ConversationSummary>)afterDelete, (Predicate<ConversationSummary>)((ConversationSummary c) => c.Id == firstId));
            Assert.Contains((IEnumerable<ConversationSummary>)afterDelete, (Predicate<ConversationSummary>)((ConversationSummary c) => c.Id == secondId));
            await service.DeleteAllConversationsAsync();
            Assert.Empty(await service.ListConversationsAsync());
        }
        finally
        {
            Environment.SetEnvironmentVariable("LOCALAPPDATA", oldLocalAppData);
            try
            {
                Directory.Delete(localAppData, recursive: true);
            }
            catch
            {
            }
        }
    }

    [Fact]
    public async Task ToolApprovalApplicationService_UsesSessionsAndPolicyFallback()
    {
        ToolAutoAcceptPolicyService policy = new ToolAutoAcceptPolicyService(() => Task.FromResult("{\"Enabled\":true,\"AllowedTools\":[\"read_file\"],\"AllowMouseTools\":false,\"AllowKeyboardTools\":false}"));
        ToolApprovalApplicationService service = new ToolApprovalApplicationService(policy);
        DateTime now = new DateTime(2026, 3, 28, 10, 0, 0, DateTimeKind.Local);
        ToolApprovalSessionState activeKeyboard = new ToolApprovalSessionState(MouseSessionActive: false, now, KeyboardSessionActive: true, now.AddMinutes(5.0));
        Assert.True((await service.DetermineAutoApproveAsync("type_text", activeKeyboard, now)).AutoApprove);
        ToolApprovalSessionState expiredMouse = new ToolApprovalSessionState(MouseSessionActive: true, now.AddMinutes(-1.0), KeyboardSessionActive: false, now);
        ToolApprovalApplicationService.ApprovalDecision expiredDecision = await service.DetermineAutoApproveAsync("click", expiredMouse, now);
        Assert.False(expiredDecision.AutoApprove);
        Assert.False(expiredDecision.SessionState.MouseSessionActive);
        Assert.Equal("Mouse session expired.", expiredDecision.SessionStatusMessage);
        Assert.True((await service.DetermineAutoApproveAsync("read_file", new ToolApprovalSessionState(MouseSessionActive: false, now, KeyboardSessionActive: false, now), now)).AutoApprove);
        Assert.True((await service.DetermineAutoApproveAsync("type_text", new ToolApprovalSessionState(MouseSessionActive: true, now.AddMinutes(2.0), KeyboardSessionActive: false, now), now)).AutoApprove);
    }

    [Fact]
    public void ToolApprovalApplicationService_AppliesSessionState_WithExplicitAndDefaultDurations()
    {
        ToolAutoAcceptPolicyService policyService = new ToolAutoAcceptPolicyService(() => Task.FromResult<string>(null));
        ToolApprovalApplicationService toolApprovalApplicationService = new ToolApprovalApplicationService(policyService);
        DateTime dateTime = new DateTime(2026, 3, 28, 12, 0, 0, DateTimeKind.Local);
        ToolApprovalSessionState toolApprovalSessionState = toolApprovalApplicationService.ApplySessionState(CreateRequest("begin_keyboard_session", "{\"duration_minutes\":3}"), new ToolApprovalSessionState(MouseSessionActive: false, dateTime, KeyboardSessionActive: false, dateTime), dateTime);
        Assert.True(toolApprovalSessionState.KeyboardSessionActive);
        Assert.Equal(dateTime.AddMinutes(3.0), toolApprovalSessionState.KeyboardSessionExpiry);
        ToolApprovalSessionState toolApprovalSessionState2 = toolApprovalApplicationService.ApplySessionState(CreateRequest("end_keyboard_session", "{}"), toolApprovalSessionState, dateTime);
        Assert.False(toolApprovalSessionState2.KeyboardSessionActive);
        ToolApprovalSessionState toolApprovalSessionState3 = toolApprovalApplicationService.ApplySessionState(CreateRequest("begin_mouse_session", "{\"duration_minutes\":-1}"), toolApprovalSessionState2, dateTime);
        Assert.True(toolApprovalSessionState3.MouseSessionActive);
        Assert.Equal(dateTime.AddMinutes(5.0), toolApprovalSessionState3.MouseSessionExpiry);
    }

    [Fact]
    public async Task ConversationAssetApplicationService_CopiesScreenshotsIntoConversationStorage()
    {
        string localAppData = Path.Combine(Path.GetTempPath(), "aire-conversation-asset-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(localAppData);
        string oldLocalAppData = Environment.GetEnvironmentVariable("LOCALAPPDATA");
        Environment.SetEnvironmentVariable("LOCALAPPDATA", localAppData);
        try
        {
            using DatabaseService db = new DatabaseService();
            await db.InitializeAsync();
            ConversationAssetApplicationService service = new ConversationAssetApplicationService(db);
            int conversationId = await db.CreateConversationAsync(1, "With Screenshot");
            string screenshotSource = Path.Combine(localAppData, "source.png");
            await File.WriteAllBytesAsync(screenshotSource, new byte[4] { 1, 2, 3, 4 });
            string root = Path.Combine(localAppData, "screenshots");
            string persistedPath = await service.PersistScreenshotAsync(now: new DateTime(2026, 3, 28, 11, 12, 13, 456, DateTimeKind.Local), conversationId: conversationId, screenshotPath: screenshotSource, screenshotsRootFolder: root);
            Assert.True(File.Exists(persistedPath));
            Assert.StartsWith(Path.Combine(root, conversationId.ToString()), persistedPath, StringComparison.OrdinalIgnoreCase);
            Assert.Contains((IEnumerable<Message>)(await db.GetMessagesAsync(conversationId)), (Predicate<Message>)((Message m) => m.Role == "assistant" && m.ImagePath == persistedPath));
        }
        finally
        {
            Environment.SetEnvironmentVariable("LOCALAPPDATA", oldLocalAppData);
            try
            {
                Directory.Delete(localAppData, recursive: true);
            }
            catch
            {
            }
        }
    }

    [Fact]
    public async Task ChatTurnApplicationService_HandlesSuccessCompletionAndDeniedToolTurns()
    {
        string localAppData = Path.Combine(Path.GetTempPath(), "aire-chat-turn-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(localAppData);
        string oldLocalAppData = Environment.GetEnvironmentVariable("LOCALAPPDATA");
        Environment.SetEnvironmentVariable("LOCALAPPDATA", localAppData);
        try
        {
            using DatabaseService db = new DatabaseService();
            await db.InitializeAsync();
            ChatSessionApplicationService chatSession = new ChatSessionApplicationService(db, db);
            ToolExecutionWorkflowService toolExecution = new ToolExecutionWorkflowService(new ToolExecutionService(new FileSystemService(), new CommandExecutionService()), db, db);
            ChatTurnApplicationService service = new ChatTurnApplicationService(chatSession, toolExecution);
            int conversationId = await db.CreateConversationAsync(1, "Chat Turn");
            ChatTurnApplicationService.SuccessTextResult success = await service.HandleSuccessTextAsync("hello there", conversationId, isWindowVisible: false, 12);
            Assert.Equal("hello there", success.FinalText);
            Assert.Equal("hello there", success.TrayPreview);
            Assert.Equal("assistant", success.AssistantHistoryMessage.Role);
            Assert.Contains((IEnumerable<Message>)(await db.GetMessagesAsync(conversationId)), (Predicate<Message>)((Message m) => m.Role == "assistant" && m.Content == "hello there"));
            ChatTurnApplicationService.CompletionResult completion = service.HandleAttemptCompletion(CreateRequest("attempt_completion", "{\"result\":\"All done\"}"));
            Assert.NotNull(completion);
            Assert.Equal("All done", completion.FinalText);
            ParsedAiResponse parsed = new ParsedAiResponse
            {
                TextContent = "Working on it",
                ToolCall = CreateRequest("read_file", "{\"path\":\"C:\\\\temp\\\\missing.txt\"}")
            };
            parsed.ToolCall.Description = "Read file: C:\\temp\\missing.txt";
            parsed.ToolCall.RawJson = "{\"tool\":\"read_file\",\"path\":\"C:\\\\temp\\\\missing.txt\"}";
            ChatTurnApplicationService.ToolExecutionTurnResult denied = await service.HandleToolExecutionAsync(parsed, approved: false, conversationId, includeScreenshotImageInHistory: false);
            Assert.Contains("<tool_call>", denied.AssistantToolCallContent, StringComparison.Ordinal);
            Assert.Equal("✗ Denied", denied.ToolCallStatus);
            Assert.Equal("assistant", denied.AssistantHistoryMessage.Role);
            Assert.Equal("user", denied.ToolHistoryMessage.Role);
            Assert.Contains("[Operation denied by user]", denied.ToolHistoryMessage.Content, StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable("LOCALAPPDATA", oldLocalAppData);
            try
            {
                Directory.Delete(localAppData, recursive: true);
            }
            catch
            {
            }
        }
    }

    [Fact]
    public void ChatInteractionApplicationService_ParsesTodoUpdatesAndFollowUpPrompts()
    {
        ChatInteractionApplicationService chatInteractionApplicationService = new ChatInteractionApplicationService();
        ParsedAiResponse parsed = new ParsedAiResponse
        {
            ToolCall = CreateRequest("update_todo_list", "{\"tasks\":[{\"id\":\"1\",\"description\":\"Review diff\",\"status\":\"completed\"},{\"id\":\"2\",\"description\":\"Write tests\",\"status\":\"pending\"}]}")
        };
        ChatInteractionApplicationService.TodoUpdateResult todoUpdateResult = chatInteractionApplicationService.BuildTodoUpdate(parsed);
        Assert.Equal(2, todoUpdateResult.Items.Count);
        Assert.Equal("Review diff", todoUpdateResult.Items[0].Description);
        Assert.Contains("1 completed", todoUpdateResult.StatusText, StringComparison.Ordinal);
        ParsedAiResponse parsed2 = new ParsedAiResponse
        {
            ToolCall = CreateRequest("ask_followup_question", "{\"question\":\"Which branch should I open?\",\"options\":[\"main\",\"release\"]}")
        };
        ChatInteractionApplicationService.FollowUpPromptResult followUpPromptResult = chatInteractionApplicationService.BuildFollowUpPrompt(parsed2);
        Assert.NotNull(followUpPromptResult);
        Assert.Equal("Which branch should I open?", followUpPromptResult.Question);
        Assert.Equal(2, followUpPromptResult.Options.Count);
        Assert.Equal("Which branch should I open?", followUpPromptResult.AssistantHistoryMessage);
    }

    [Fact]
    public async Task ProviderActivationApplicationService_PersistsSelectionAndBuildsConversationPlan()
    {
        string localAppData = Path.Combine(Path.GetTempPath(), "aire-provider-activation-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(localAppData);
        string oldLocalAppData = Environment.GetEnvironmentVariable("LOCALAPPDATA");
        Environment.SetEnvironmentVariable("LOCALAPPDATA", localAppData);
        try
        {
            using DatabaseService db = new DatabaseService();
            await db.InitializeAsync();
            Provider providerOne = new Provider
            {
                Name = "Provider One",
                Type = "OpenAI",
                ApiKey = "sk-one",
                Model = "gpt-4.1-mini",
                IsEnabled = true,
                Color = "#123456"
            };
            Provider provider = providerOne;
            provider.Id = await db.InsertProviderAsync(providerOne);
            Provider providerTwo = new Provider
            {
                Name = "Provider Two",
                Type = "OpenAI",
                ApiKey = "sk-two",
                Model = "gpt-4o-mini",
                IsEnabled = true,
                Color = "#654321"
            };
            Provider provider2 = providerTwo;
            provider2.Id = await db.InsertProviderAsync(providerTwo);
            int currentConversationId = await db.CreateConversationAsync(providerOne.Id, "Current");
            await db.CreateConversationAsync(providerTwo.Id, "Latest Two");
            ChatSessionApplicationService chatSession = new ChatSessionApplicationService(db, db);
            ProviderActivationApplicationService service = new ProviderActivationApplicationService(new ChatService(new ProviderFactory(db)), new ProviderFactory(db), chatSession);
            ProviderActivationApplicationService.ProviderActivationResult loadExisting = await service.ActivateProviderAsync(providerTwo, providerOne.Id, null, showSwitchedMessage: true);
            Assert.Equal(ProviderActivationWorkflowService.ConversationActionKind.LoadExistingConversation, loadExisting.ActivationPlan.ConversationAction);
            Assert.NotNull(loadExisting.ActivationPlan.ConversationIdToLoad);
            Aire.Data.Conversation loadedConversation = await db.GetConversationAsync(loadExisting.ActivationPlan.ConversationIdToLoad.Value);
            Assert.NotNull(loadedConversation);
            Assert.Equal(providerTwo.Id, loadedConversation.ProviderId);
            ProviderActivationApplicationService.ProviderActivationResult keepCurrent = await service.ActivateProviderAsync(providerTwo, providerOne.Id, currentConversationId, showSwitchedMessage: true);
            Assert.NotNull(keepCurrent.ProviderInstance);
            Assert.Equal(ProviderActivationWorkflowService.ConversationActionKind.KeepCurrentConversation, keepCurrent.ActivationPlan.ConversationAction);
            Assert.True(keepCurrent.ActivationPlan.ShouldAnnounceSwitch);
            Assert.Contains("Provider Two", keepCurrent.SwitchedProviderMessage, StringComparison.Ordinal);
            int? expected = providerTwo.Id;
            Assert.Equal(expected, await chatSession.GetSelectedProviderIdAsync());
            Aire.Data.Conversation updatedConversation = await db.GetConversationAsync(currentConversationId);
            Assert.NotNull(updatedConversation);
            Assert.Equal(providerTwo.Id, updatedConversation.ProviderId);
        }
        finally
        {
            Environment.SetEnvironmentVariable("LOCALAPPDATA", oldLocalAppData);
            try
            {
                Directory.Delete(localAppData, recursive: true);
            }
            catch
            {
            }
        }
    }

    [Fact]
    public async Task ProviderCatalogApplicationService_LoadsEnabledProvidersAndResolvesSelection()
    {
        Provider providerOne = new Provider
        {
            Id = 101,
            Name = "Enabled One",
            Type = "OpenAI",
            ApiKey = "sk-one",
            Model = "gpt-4.1-mini",
            IsEnabled = true,
            Color = "#123456"
        };
        Provider providerTwo = new Provider
        {
            Id = 202,
            Name = "Disabled Two",
            Type = "OpenAI",
            ApiKey = "sk-two",
            Model = "gpt-4o-mini",
            IsEnabled = false,
            Color = "#654321"
        };
        InMemoryProviderRepository repository = new InMemoryProviderRepository(providerOne, providerTwo);
        ProviderCatalogApplicationService service = new ProviderCatalogApplicationService(repository);
        ProviderCatalogApplicationService.ProviderCatalogResult catalog = await service.LoadProviderCatalogAsync(autoSelect: true, providerOne.Id);
        Assert.Equal(2, catalog.AllProviders.Count);
        Assert.Single(catalog.EnabledProviders);
        Assert.Equal(providerOne.Id, catalog.SelectedProvider?.Id);
        Assert.Null(catalog.EmptyStateMessage);
        Provider refreshedSelection = service.ResolveSelectionAfterRefresh(catalog.EnabledProviders, providerOne.Id);
        Assert.NotNull(refreshedSelection);
        Assert.Equal(providerOne.Id, refreshedSelection.Id);
        providerOne.IsEnabled = false;
        await repository.UpdateProviderAsync(providerOne);
        ProviderCatalogApplicationService.ProviderCatalogResult emptyCatalog = await service.LoadProviderCatalogAsync(autoSelect: true, providerOne.Id);
        Assert.Empty(emptyCatalog.EnabledProviders);
        Assert.Null(emptyCatalog.SelectedProvider);
        Assert.Contains("No supported AI providers", emptyCatalog.EmptyStateMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProviderCatalogApplicationService_HonorsAutoSelectFlag_AndMissingRefreshSelection()
    {
        Provider provider = new Provider
        {
            Id = 303,
            Name = "Enabled",
            Type = "OpenAI",
            ApiKey = "sk-test",
            Model = "gpt-4.1-mini",
            IsEnabled = true
        };
        ProviderCatalogApplicationService service = new ProviderCatalogApplicationService(new InMemoryProviderRepository(provider));
        ProviderCatalogApplicationService.ProviderCatalogResult manualCatalog = await service.LoadProviderCatalogAsync(autoSelect: false, null);
        Assert.Single(manualCatalog.EnabledProviders);
        Assert.Null(manualCatalog.SelectedProvider);
        Provider missingSelection = service.ResolveSelectionAfterRefresh(manualCatalog.EnabledProviders, 999);
        Assert.Null(missingSelection);
    }

    [Fact]
    public async Task ProviderSetupApplicationService_BuildsSmokeTestsAndSavesProviders()
    {
        ProviderSetupApplicationService service = new ProviderSetupApplicationService();
        IAiProvider provider = service.BuildRuntimeProvider(new ProviderRuntimeRequest("OpenAI", "sk-test", "https://api.example.com", "gpt-4.1-mini", ClaudeWebSessionReady: false));
        Assert.NotNull(provider);
        ProviderSmokeTestResult smoke = await service.RunSmokeTestAsync(new StubSmokeTestProvider(success: false, "boom"), CancellationToken.None);
        Assert.False(smoke.Success);
        Assert.Equal("boom", smoke.ErrorMessage);
        InMemoryProviderRepository repository = new InMemoryProviderRepository();
        ProviderPersistResult saved = await service.SaveNewProviderAsync(repository, new ProviderDraft("Provider A", "OpenAI", "sk-test", "https://api.example.com", "gpt-4.1-mini"));
        Assert.True(saved.Saved);
        Assert.False(saved.IsDuplicate);
        ProviderPersistResult duplicate = await service.SaveNewProviderAsync(repository, new ProviderDraft("Provider B", "OpenAI", "sk-other", "https://api.example.com", "gpt-4.1-mini"));
        Assert.False(duplicate.Saved);
        Assert.True(duplicate.IsDuplicate);
        IAiProvider codexProvider = service.BuildRuntimeProvider(new ProviderRuntimeRequest("Codex", null, null, "default", ClaudeWebSessionReady: false));
        Assert.NotNull(codexProvider);
        Assert.Equal("Codex", codexProvider.ProviderType);
    }

    [Fact]
    public async Task ProviderRuntimeApplicationService_UsesRuntimeGateway()
    {
        StubSmokeTestProvider fakeProvider = new StubSmokeTestProvider(success: true);
        FakeProviderRuntimeGateway gateway = new FakeProviderRuntimeGateway
        {
            ProviderToReturn = fakeProvider,
            SmokeTestResult = new ProviderSmokeTestResult(Success: false, "gateway failure")
        };
        ProviderRuntimeApplicationService service = new ProviderRuntimeApplicationService(gateway);
        IAiProvider provider = service.BuildProvider(new ProviderRuntimeRequest("OpenAI", "sk-test", null, "gpt-4.1-mini", ClaudeWebSessionReady: false));
        Assert.Same(fakeProvider, provider);
        ProviderSmokeTestResult smoke = await service.RunSmokeTestAsync(fakeProvider, CancellationToken.None);
        Assert.False(smoke.Success);
        Assert.Equal("gateway failure", smoke.ErrorMessage);
        Assert.Same(fakeProvider, gateway.LastSmokeTestProvider);
    }

    [Fact]
    public async Task ProviderCapabilityTestSessionService_LoadsAndSavesSessions()
    {
        InMemorySettingsRepository repository = new InMemorySettingsRepository();
        ProviderCapabilityTestSessionService service = new ProviderCapabilityTestSessionService();
        DateTime testedAt = new DateTime(2026, 3, 28, 10, 30, 0, DateTimeKind.Utc);
        List<CapabilityTestResult> results = new List<CapabilityTestResult>
        {
            new CapabilityTestResult("ok", "List directory", "File System", Passed: true, "list_directory", null, 1200L),
            new CapabilityTestResult("bad", "Fetch URL", "Web", Passed: false, null, "no tool", 800L)
        };
        await service.SaveAsync(42, "gpt-4.1-mini", results, testedAt, repository);
        CapabilityTestSession loaded = await service.LoadAsync(42, "gpt-4.1-mini", repository);
        Assert.NotNull(loaded);
        Assert.Equal("gpt-4.1-mini", loaded.Model);
        Assert.Equal(testedAt, loaded.TestedAt);
        Assert.Equal(2, loaded.Results.Count);
        Assert.Null(await service.LoadAsync(42, "missing-model", repository));
    }

    [Fact]
    public async Task ProviderCapabilityTestSessionService_RecoversFromCorruptedStoredJson()
    {
        InMemorySettingsRepository repository = new InMemorySettingsRepository();
        ProviderCapabilityTestSessionService service = new ProviderCapabilityTestSessionService();
        await repository.SetSettingAsync("capability_tests_42", "{not-json");
        Assert.Null(await service.LoadAsync(42, "gpt-4.1-mini", repository));
        await service.SaveAsync(42, "gpt-4.1-mini", new CapabilityTestResult[1]
        {
            new CapabilityTestResult("ok", "Ping", "General", Passed: true, "noop", null, 10L)
        }, new DateTime(2026, 3, 28, 10, 45, 0, DateTimeKind.Utc), repository);
        CapabilityTestSession recovered = await service.LoadAsync(42, "gpt-4.1-mini", repository);
        Assert.NotNull(recovered);
        Assert.Single(recovered.Results);
    }

    [Fact]
    public async Task ProviderCapabilityTestApplicationService_RunsPersistsAndReportsProgress()
    {
        InMemorySettingsRepository settings = new InMemorySettingsRepository();
        ProviderCapabilityTestApplicationService service = new ProviderCapabilityTestApplicationService();
        List<ProviderCapabilityTestApplicationService.ProgressUpdate> progressUpdates = new List<ProviderCapabilityTestApplicationService.ProgressUpdate>();
        Assert.Equal(2, (await service.RunAndPersistAsync(new StubSmokeTestProvider(success: true), 77, "gpt-4.1-mini", RunAllAsync, settings, new Progress<ProviderCapabilityTestApplicationService.ProgressUpdate>(delegate (ProviderCapabilityTestApplicationService.ProgressUpdate update)
        {
            progressUpdates.Add(update);
        }), CancellationToken.None)).Results.Count);
        Assert.Equal(2, progressUpdates.Count);
        Assert.Equal(2, progressUpdates[1].CompletedCount);
        CapabilityTestSession session = await new ProviderCapabilityTestSessionService().LoadAsync(77, "gpt-4.1-mini", settings);
        Assert.NotNull(session);
        Assert.Equal(2, session.Results.Count);
        static async IAsyncEnumerable<CapabilityTestResult> RunAllAsync(IAiProvider provider, [EnumeratorCancellation] CancellationToken ct)
        {
            yield return new CapabilityTestResult("a", "First", "Cat", Passed: true, "list_directory", null, 100L);
            await Task.Yield();
            yield return new CapabilityTestResult("b", "Second", "Cat", Passed: false, null, "failed", 200L);
        }
    }

    [Fact]
    public async Task OnboardingProviderSetupApplicationService_CompletesAndBlocksDuplicates()
    {
        OnboardingProviderSetupApplicationService service = new OnboardingProviderSetupApplicationService();
        InMemoryProviderRepository repository = new InMemoryProviderRepository();
        OnboardingProviderSetupApplicationService.Step3Result saved = await service.CompleteStepAsync(repository, new OnboardingProviderSetupApplicationService.Step3Request("Claude Browser", "ClaudeWeb", null, null, "claude-sonnet", null, ClaudeWebSessionReady: true));
        Assert.True(saved.ShouldAdvance);
        Assert.True(saved.SavedProvider);
        Assert.False(saved.IsDuplicate);
        OnboardingProviderSetupApplicationService.Step3Result duplicate = await service.CompleteStepAsync(repository, new OnboardingProviderSetupApplicationService.Step3Request("Claude Browser 2", "ClaudeWeb", null, null, "claude-sonnet", null, ClaudeWebSessionReady: true));
        Assert.False(duplicate.ShouldAdvance);
        Assert.True(duplicate.IsDuplicate);
        OnboardingProviderSetupApplicationService.Step3Result noCredential = await service.CompleteStepAsync(repository, new OnboardingProviderSetupApplicationService.Step3Request("OpenAI", "OpenAI", null, null, "gpt-4.1-mini", null, ClaudeWebSessionReady: false));
        Assert.True(noCredential.ShouldAdvance);
        Assert.False(noCredential.SavedProvider);
        Assert.False(noCredential.IsDuplicate);
    }

    [Fact]
    public async Task OnboardingProviderSetupApplicationService_DoesNotSave_WhenNameIsMissing()
    {
        OnboardingProviderSetupApplicationService service = new OnboardingProviderSetupApplicationService();
        InMemoryProviderRepository repository = new InMemoryProviderRepository();
        OnboardingProviderSetupApplicationService.Step3Result result = await service.CompleteStepAsync(repository, new OnboardingProviderSetupApplicationService.Step3Request("", "OpenAI", "sk-test", "https://api.example.com", "gpt-4.1-mini", null, ClaudeWebSessionReady: false));
        Assert.True(result.ShouldAdvance);
        Assert.False(result.SavedProvider);
        Assert.Empty(await repository.GetProvidersAsync());
    }

    [Fact]
    public async Task SwitchModelApplicationService_SwitchesProviderAndRejectsMissingModels()
    {
        string localAppData = Path.Combine(Path.GetTempPath(), "aire-switch-model-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(localAppData);
        string oldLocalAppData = Environment.GetEnvironmentVariable("LOCALAPPDATA");
        Environment.SetEnvironmentVariable("LOCALAPPDATA", localAppData);
        try
        {
            using DatabaseService db = new DatabaseService();
            await db.InitializeAsync();
            Provider providerOne = new Provider
            {
                Name = "Provider One",
                Type = "OpenAI",
                ApiKey = "sk-one",
                Model = "gpt-4.1-mini",
                IsEnabled = true
            };
            Provider provider = providerOne;
            provider.Id = await db.InsertProviderAsync(providerOne);
            Provider providerTwo = new Provider
            {
                Name = "Provider Two",
                Type = "OpenAI",
                ApiKey = "sk-two",
                Model = "gpt-4o-mini",
                IsEnabled = true
            };
            Provider provider2 = providerTwo;
            provider2.Id = await db.InsertProviderAsync(providerTwo);
            int conversationId = await db.CreateConversationAsync(providerOne.Id, "Switch Test");
            ChatSessionApplicationService chatSession = new ChatSessionApplicationService(db, db);
            SwitchModelApplicationService service = new SwitchModelApplicationService(new ProviderFactory(db), new ChatService(new ProviderFactory(db)), chatSession);
            ParsedAiResponse parsed = new ParsedAiResponse
            {
                TextContent = "Switching now",
                ToolCall = CreateRequest("switch_model", "{\"model_name\":\"gpt-4o-mini\",\"reason\":\"Need a different model\"}")
            };
            parsed.ToolCall.RawJson = "{\"tool\":\"switch_model\",\"parameters\":{\"model_name\":\"gpt-4o-mini\",\"reason\":\"Need a different model\"}}";
            SwitchModelApplicationService.SwitchModelResult result = await service.ExecuteAsync(parsed, new Provider[2] { providerOne, providerTwo }, (int _) => false, conversationId);
            Assert.True(result.Succeeded);
            Assert.NotNull(result.TargetProvider);
            Assert.Equal(providerTwo.Id, result.TargetProvider.Id);
            Assert.Contains("SUCCESS", result.ResultHistoryMessage.Content, StringComparison.Ordinal);
            int? expected = providerTwo.Id;
            Assert.Equal(expected, await chatSession.GetSelectedProviderIdAsync());
            Aire.Data.Conversation updatedConversation = await db.GetConversationAsync(conversationId);
            Assert.NotNull(updatedConversation);
            Assert.Equal(providerTwo.Id, updatedConversation.ProviderId);
            ParsedAiResponse missingParsed = new ParsedAiResponse
            {
                ToolCall = CreateRequest("switch_model", "{\"model_name\":\"missing-model\"}")
            };
            missingParsed.ToolCall.RawJson = "{\"tool\":\"switch_model\",\"parameters\":{\"model_name\":\"missing-model\"}}";
            SwitchModelApplicationService.SwitchModelResult missing = await service.ExecuteAsync(missingParsed, new Provider[2] { providerOne, providerTwo }, (int _) => false, conversationId);
            Assert.False(missing.Succeeded);
            Assert.Contains("no available provider", missing.ResultHistoryMessage.Content, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Environment.SetEnvironmentVariable("LOCALAPPDATA", oldLocalAppData);
            try
            {
                Directory.Delete(localAppData, recursive: true);
            }
            catch
            {
            }
        }
    }

    [Fact]
    public void ProviderEditorApplicationService_BuildsSelectionAndOllamaPlans()
    {
        ProviderEditorApplicationService providerEditorApplicationService = new ProviderEditorApplicationService();
        Provider provider = new Provider
        {
            Name = "Ollama Local",
            Type = "Ollama",
            ApiKey = null,
            BaseUrl = "http://localhost:11434",
            Model = "qwen2.5:7b",
            IsEnabled = true
        };
        ProviderEditorApplicationService.ProviderEditorSelectionPlan providerEditorSelectionPlan = providerEditorApplicationService.BuildSelectionPlan(provider, isRefreshing: false);
        Assert.Equal("Ollama Local", providerEditorSelectionPlan.Name);
        Assert.Equal(ProviderEditorApplicationService.ModelLoadAction.LoadOllamaModels, providerEditorSelectionPlan.ModelAction);
        Assert.False(providerEditorSelectionPlan.HasApiKey);
        ProviderEditorApplicationService.ProviderEditorSelectionPlan providerEditorSelectionPlan2 = providerEditorApplicationService.BuildSelectionPlan(provider, isRefreshing: true);
        Assert.Equal(ProviderEditorApplicationService.ModelLoadAction.SyncExistingOllamaItems, providerEditorSelectionPlan2.ModelAction);
        ProviderEditorApplicationService.OllamaSelectionPlan ollamaSelectionPlan = providerEditorApplicationService.BuildOllamaSelectionPlan(new string[] { "qwen2.5:7b", "mistral:7b" }, "qwen2.5:7b");
        Assert.Equal("qwen2.5:7b", ollamaSelectionPlan.SelectedModelName);
        Assert.False(ollamaSelectionPlan.EnableDownloadButton);
        ProviderEditorApplicationService.OllamaSelectionPlan ollamaSelectionPlan2 = providerEditorApplicationService.BuildOllamaSelectionPlan(new string[] { "mistral:7b" }, "qwen2.5:7b");
        Assert.Null(ollamaSelectionPlan2.SelectedModelName);
        Assert.True(ollamaSelectionPlan2.EnableDownloadButton);
        Provider provider2 = new Provider
        {
            Name = "Cloud",
            Type = "OpenAI",
            ApiKey = "sk-test",
            BaseUrl = "https://api.example.com",
            Model = "gpt-4.1-mini",
            IsEnabled = true
        };
        ProviderEditorApplicationService.ProviderEditorSelectionPlan providerEditorSelectionPlan3 = providerEditorApplicationService.BuildSelectionPlan(provider2, isRefreshing: false);
        Assert.Equal(ProviderEditorApplicationService.ModelLoadAction.LoadMetadataModels, providerEditorSelectionPlan3.ModelAction);
        Assert.True(providerEditorSelectionPlan3.HasApiKey);
    }

    [Fact]
    public async Task ProviderEditorSaveApplicationService_AppliesAndPersistsEditorValues()
    {
        Provider provider = new Provider
        {
            Id = 15,
            Name = "Old Name",
            Type = "Ollama",
            ApiKey = null,
            BaseUrl = "http://old",
            Model = "old-model",
            IsEnabled = true,
            TimeoutMinutes = 5
        };
        InMemoryProviderRepository repository = new InMemoryProviderRepository(provider);
        ProviderEditorSaveApplicationService service = new ProviderEditorSaveApplicationService();
        await service.SaveAsync(new ProviderEditorSaveApplicationService.SaveRequest(provider, "New Name", "Ollama", null, "http://localhost:11434", "Ã¢\u02dc… qwen2.5:7b  (4.7 GB)", null, 12, IsEnabled: true, new (string, string)[1] { ("Ã¢\u02dc… qwen2.5:7b  (4.7 GB)", "qwen2.5:7b") }), repository);
        Provider saved = (await repository.GetProvidersAsync()).Single((Provider p) => p.Id == 15);
        Assert.Equal("New Name", saved.Name);
        Assert.Equal("http://localhost:11434", saved.BaseUrl);
        Assert.Equal("qwen2.5:7b", saved.Model);
        Assert.Equal(12, saved.TimeoutMinutes);
    }

    [Fact]
    public async Task SettingsProviderListApplicationService_LoadsCreatesDeletesAndReordersProviders()
    {
        Provider first = new Provider
        {
            Id = 1,
            Name = "First",
            Type = "OpenAI",
            Model = "gpt-4.1",
            IsEnabled = true
        };
        Provider second = new Provider
        {
            Id = 2,
            Name = "Second",
            Type = "Ollama",
            Model = "qwen2.5:7b",
            IsEnabled = true
        };
        InMemoryProviderRepository repository = new InMemoryProviderRepository(first, second);
        SettingsProviderListApplicationService service = new SettingsProviderListApplicationService();
        int? currentSelectedId = 2;
        SettingsProviderListApplicationService.ProviderListState initial = await service.LoadAsync(repository, null, currentSelectedId);
        Assert.Equal(2, initial.Providers.Count);
        Assert.Equal(2, initial.SelectedProvider?.Id);
        Provider created = await service.CreateDefaultProviderAsync(repository);
        Assert.Equal("OpenAI", created.Name);
        Assert.Equal("OpenAI", created.Type);
        SettingsProviderListApplicationService.ProviderListState refreshed = await service.LoadAsync(repository, created.Id);
        Assert.Equal(created.Id, refreshed.SelectedProvider?.Id);
        SettingsProviderListApplicationService.ReorderResult reorder = service.Reorder(refreshed.Providers, created.Id, 1);
        Assert.True(reorder.OrderChanged);
        Assert.Equal(created.Id, reorder.SelectedProvider?.Id);
        Assert.Equal(created.Id, reorder.Providers[0].Id);
        await service.SaveOrderAsync(repository, reorder.Providers);
        Assert.Equal(actual: (await repository.GetProvidersAsync())[0].Id, expected: created.Id);
        await service.DeleteProviderAsync(repository, created.Id);
        currentSelectedId = created.Id;
        SettingsProviderListApplicationService.ProviderListState afterDelete = await service.LoadAsync(repository, null, currentSelectedId);
        Assert.Equal(2, afterDelete.Providers.Count);
        Assert.Null(afterDelete.SelectedProvider);
    }

    [Fact]
    public async Task SettingsProviderListApplicationService_IgnoresNoOpReorders_AndKeepsExplicitReselection()
    {
        Provider provider = new Provider
        {
            Id = 10,
            Name = "Only",
            Type = "OpenAI",
            Model = "gpt-4.1",
            IsEnabled = true
        };
        InMemoryProviderRepository repository = new InMemoryProviderRepository(provider);
        SettingsProviderListApplicationService service = new SettingsProviderListApplicationService();
        SettingsProviderListApplicationService.ProviderListState state = await service.LoadAsync(repository, 10, 999);
        Assert.Equal(10, state.SelectedProvider?.Id);
        SettingsProviderListApplicationService.ReorderResult sameTarget = service.Reorder(state.Providers, 10, 10);
        Assert.False(sameTarget.OrderChanged);
        Assert.Equal(10, sameTarget.SelectedProvider?.Id);
        SettingsProviderListApplicationService.ReorderResult missingTarget = service.Reorder(state.Providers, 10, 999);
        Assert.False(missingTarget.OrderChanged);
        Assert.Equal(10, missingTarget.SelectedProvider?.Id);
    }

    [Fact]
    public void LocalApiApplicationService_BuildsApiPlansAndResults()
    {
        LocalApiApplicationService localApiApplicationService = new LocalApiApplicationService();
        Provider provider = new Provider
        {
            Id = 7,
            Name = "OpenAI Main",
            Type = "OpenAI",
            Model = "gpt-4.1",
            IsEnabled = true,
            Color = "#123456"
        };
        List<ApiProviderSnapshot> list = localApiApplicationService.BuildProviderSnapshots(new Provider[1] { provider });
        Assert.Single(list);
        Assert.Equal(provider.Id, list[0].Id);
        Assert.Equal(provider.Name, list[0].Name);
        LocalApiApplicationService.ConversationCreationPlan conversationCreationPlan = localApiApplicationService.BuildConversationCreationPlan(provider.Name, "  Fresh Chat  ");
        Assert.Equal("Fresh Chat", conversationCreationPlan.Title);
        Assert.Contains(provider.Name, conversationCreationPlan.SystemMessage, StringComparison.Ordinal);
        LocalApiApplicationService.ConversationCreationPlan conversationCreationPlan2 = localApiApplicationService.BuildConversationCreationPlan(provider.Name, null);
        Assert.Equal("New Chat", conversationCreationPlan2.Title);
        Provider provider2 = localApiApplicationService.ResolveConversationProvider(new Aire.Data.Conversation
        {
            Id = 5,
            ProviderId = provider.Id,
            Title = "Test"
        }, new Provider[1] { provider });
        Assert.Equal(provider.Id, provider2?.Id);
        Assert.Null(localApiApplicationService.ResolveConversationProvider(null, new Provider[1] { provider }));
        Assert.Equal("gpt-4o-mini", localApiApplicationService.NormalizeProviderModel("  gpt-4o-mini  "));
        Assert.Null(localApiApplicationService.NormalizeProviderModel("   "));
        using JsonDocument jsonDocument = JsonDocument.Parse("{\"path\":\"C:\\\\temp\\\\note.txt\"}");
        ToolCallRequest toolCallRequest = localApiApplicationService.BuildToolRequest("read_file", jsonDocument.RootElement.Clone());
        Assert.Equal("read_file", toolCallRequest.Tool);
        Assert.Contains("\"tool\":\"read_file\"", toolCallRequest.RawJson, StringComparison.Ordinal);
        using JsonDocument jsonDocument2 = JsonDocument.Parse("\"text\"");
        ToolCallRequest toolCallRequest2 = localApiApplicationService.BuildToolRequest("CLICK", jsonDocument2.RootElement.Clone());
        Assert.Equal("CLICK", toolCallRequest2.Tool);
        Assert.Equal(JsonValueKind.Object, toolCallRequest2.Parameters.ValueKind);
        ApiToolExecutionResult apiToolExecutionResult = localApiApplicationService.BuildPendingApprovalResult("read_file", 4);
        Assert.Equal("pending_approval", apiToolExecutionResult.Status);
        Assert.Equal(4, apiToolExecutionResult.PendingApprovalIndex);
        ApiToolExecutionResult apiToolExecutionResult2 = localApiApplicationService.BuildCompletedToolResult(new ToolExecutionResult
        {
            TextResult = "Done",
            DirectoryListing = new DirectoryListing
            {
                Path = "C:\\temp",
                Entries =
                {
                    new DirectoryEntry
                    {
                        IsDirectory = true,
                        Name = "docs"
                    },
                    new DirectoryEntry
                    {
                        IsDirectory = false,
                        Name = "note.txt"
                    }
                }
            },
            ScreenshotPath = "C:\\temp\\capture.png"
        });
        Assert.Equal("completed", apiToolExecutionResult2.Status);
        Assert.Equal("Done", apiToolExecutionResult2.TextResult);
        Assert.Equal("C:\\temp", apiToolExecutionResult2.DirectoryPath);
        Assert.Equal("1 folder, 1 file", apiToolExecutionResult2.DirectorySummary);
        Assert.Equal("C:\\temp\\capture.png", apiToolExecutionResult2.ScreenshotPath);
        ApiStateSnapshot apiStateSnapshot = localApiApplicationService.BuildStateSnapshot(8200, isStartupReady: true, isMainWindowVisible: false, isSettingsOpen: true, isBrowserOpen: false, apiAccessEnabled: true, hasApiAccessToken: true, 12, provider, 3);
        Assert.Equal(8200, apiStateSnapshot.LocalApiPort);
        Assert.Equal(12, apiStateSnapshot.CurrentConversationId);
        Assert.Equal(provider.Id, apiStateSnapshot.CurrentProviderId);
        Assert.Equal(3, apiStateSnapshot.PendingApprovals);
    }

    [Fact]
    public void LocalApiApplicationService_NormalizesEmptyModels_AndNonObjectParametersSafely()
    {
        LocalApiApplicationService localApiApplicationService = new LocalApiApplicationService();
        Assert.Null(localApiApplicationService.NormalizeProviderModel("   "));
        Assert.Equal("gpt-4.1-mini", localApiApplicationService.NormalizeProviderModel("  gpt-4.1-mini  "));
        Assert.Null(localApiApplicationService.ResolveConversationProvider(new Aire.Data.Conversation
        {
            Id = 6,
            ProviderId = 99,
            Title = "Missing provider"
        }, Array.Empty<Provider>()));
        using JsonDocument jsonDocument = JsonDocument.Parse("[\"not\",\"an\",\"object\"]");
        ToolCallRequest toolCallRequest = localApiApplicationService.BuildToolRequest("read_file", jsonDocument.RootElement.Clone());
        Assert.Equal(JsonValueKind.Object, toolCallRequest.Parameters.ValueKind);
        Assert.Contains("\"parameters\":{}", toolCallRequest.RawJson, StringComparison.Ordinal);
        ApiToolExecutionResult apiToolExecutionResult = localApiApplicationService.BuildPendingApprovalResult("click", null);
        Assert.Equal("pending_approval", apiToolExecutionResult.Status);
        Assert.Null(apiToolExecutionResult.PendingApprovalIndex);
    }

    [Fact]
    public void ConversationTranscriptApplicationService_BuildsTranscriptHistoryAndInputState()
    {
        ConversationTranscriptApplicationService conversationTranscriptApplicationService = new ConversationTranscriptApplicationService();
        DateTime createdAt = new DateTime(2026, 3, 28, 10, 0, 0, DateTimeKind.Utc);
        DateTime createdAt2 = createdAt.AddDays(1.0);
        List<Message> messages = new List<Message>
        {
            new Message
            {
                Role = "user",
                Content = "Hello",
                CreatedAt = createdAt
            },
            new Message
            {
                Role = "assistant",
                Content = "Hi there",
                CreatedAt = createdAt.AddMinutes(1.0)
            },
            new Message
            {
                Role = "tool",
                Content = "✓ read_file",
                CreatedAt = createdAt.AddMinutes(2.0)
            },
            new Message
            {
                Role = "user",
                Content = "Hello",
                CreatedAt = createdAt.AddMinutes(3.0)
            },
            new Message
            {
                Role = "assistant",
                Content = "Next day",
                CreatedAt = createdAt2
            }
        };
        ConversationTranscriptApplicationService.ConversationTranscriptPlan conversationTranscriptPlan = conversationTranscriptApplicationService.BuildTranscript(messages);
        Assert.Equal(5, conversationTranscriptPlan.Entries.Count);
        Assert.True(conversationTranscriptPlan.Entries[0].StartsNewDateSection);
        Assert.False(conversationTranscriptPlan.Entries[1].StartsNewDateSection);
        Assert.True(conversationTranscriptPlan.Entries[4].StartsNewDateSection);
        Assert.Equal(4, conversationTranscriptPlan.ConversationHistory.Count);
        Assert.Single(conversationTranscriptPlan.InputHistory);
        Assert.Equal("Hello", conversationTranscriptPlan.InputHistory[0]);
        Assert.Equal("AI", conversationTranscriptPlan.Entries[2].Sender);
        Assert.Equal(ConversationTranscriptApplicationService.TranscriptRole.Tool, conversationTranscriptPlan.Entries[2].Role);
    }

    [Fact]
    public void ConversationTranscriptApplicationService_HandlesSystemMessages_AndDeduplicatesSequentialInput()
    {
        ConversationTranscriptApplicationService conversationTranscriptApplicationService = new ConversationTranscriptApplicationService();
        DateTime createdAt = new DateTime(2026, 3, 28, 9, 0, 0, DateTimeKind.Local);
        ConversationTranscriptApplicationService.ConversationTranscriptPlan conversationTranscriptPlan = conversationTranscriptApplicationService.BuildTranscript(new Message[4]
        {
            new Message
            {
                Role = "system",
                Content = "Started",
                CreatedAt = createdAt
            },
            new Message
            {
                Role = "user",
                Content = "Hello",
                CreatedAt = createdAt.AddMinutes(1.0)
            },
            new Message
            {
                Role = "user",
                Content = "Hello",
                CreatedAt = createdAt.AddMinutes(2.0)
            },
            new Message
            {
                Role = "assistant",
                Content = "Hi",
                CreatedAt = createdAt.AddDays(1.0)
            }
        });
        Assert.Equal(4, conversationTranscriptPlan.Entries.Count);
        Assert.Equal(ConversationTranscriptApplicationService.TranscriptRole.System, conversationTranscriptPlan.Entries[0].Role);
        Assert.False(conversationTranscriptPlan.Entries[0].StartsNewDateSection);
        Assert.True(conversationTranscriptPlan.Entries[1].StartsNewDateSection);
        Assert.True(conversationTranscriptPlan.Entries[3].StartsNewDateSection);
        Assert.Single(conversationTranscriptPlan.InputHistory);
        Assert.Equal(3, conversationTranscriptPlan.ConversationHistory.Count);
    }

    [Fact]
    public void ToolApprovalPromptApplicationService_BuildsPromptAndCompletionPlans()
    {
        ToolApprovalPromptApplicationService toolApprovalPromptApplicationService = new ToolApprovalPromptApplicationService();
        ToolApprovalPromptApplicationService.ApprovalPromptPlan approvalPromptPlan = toolApprovalPromptApplicationService.BuildPromptPlan(autoApprove: true, isWindowVisible: false);
        Assert.False(approvalPromptPlan.IsApprovalPending);
        Assert.True(approvalPromptPlan.AutoApproveImmediately);
        Assert.False(approvalPromptPlan.ShouldRevealWindow);
        ToolApprovalPromptApplicationService.ApprovalPromptPlan approvalPromptPlan2 = toolApprovalPromptApplicationService.BuildPromptPlan(autoApprove: false, isWindowVisible: false);
        Assert.True(approvalPromptPlan2.IsApprovalPending);
        Assert.False(approvalPromptPlan2.AutoApproveImmediately);
        Assert.True(approvalPromptPlan2.ShouldRevealWindow);
        ToolApprovalPromptApplicationService.ApprovalCompletionPlan approvalCompletionPlan = toolApprovalPromptApplicationService.BuildCompletionPlan(approved: true, "read_file");
        Assert.False(approvalCompletionPlan.WasDenied);
        Assert.Equal("✓ read_file", approvalCompletionPlan.ToolCallStatus);
        ToolApprovalPromptApplicationService.ApprovalCompletionPlan approvalCompletionPlan2 = toolApprovalPromptApplicationService.BuildCompletionPlan(approved: false, "read_file");
        Assert.True(approvalCompletionPlan2.WasDenied);
        Assert.Equal("✗ Denied", approvalCompletionPlan2.ToolCallStatus);
    }

    [Fact]
    public void ToolApprovalPromptApplicationService_DoesNotRevealVisiblePendingPrompts()
    {
        ToolApprovalPromptApplicationService toolApprovalPromptApplicationService = new ToolApprovalPromptApplicationService();
        ToolApprovalPromptApplicationService.ApprovalPromptPlan approvalPromptPlan = toolApprovalPromptApplicationService.BuildPromptPlan(autoApprove: false, isWindowVisible: true);
        Assert.True(approvalPromptPlan.IsApprovalPending);
        Assert.False(approvalPromptPlan.AutoApproveImmediately);
        Assert.False(approvalPromptPlan.ShouldRevealWindow);
        ToolApprovalPromptApplicationService.ApprovalCompletionPlan approvalCompletionPlan = toolApprovalPromptApplicationService.BuildCompletionPlan(approved: true, "List directory");
        Assert.False(approvalCompletionPlan.WasDenied);
        Assert.Equal("✓ List directory", approvalCompletionPlan.ToolCallStatus);
    }

    [Fact]
    public void ProviderUiStateApplicationService_BuildsCapabilityAvailabilityAndTokenUsageState()
    {
        ProviderUiStateApplicationService providerUiStateApplicationService = new ProviderUiStateApplicationService();
        ProviderUiStateApplicationService.CapabilityUiState capabilityUiState = providerUiStateApplicationService.BuildCapabilityUiState(new StubProviderWithCapabilities(ProviderCapabilities.ImageInput | ProviderCapabilities.ToolCalling), speechHasMic: true, speechModelExists: false, null);
        Assert.True(capabilityUiState.CanImages);
        Assert.True(capabilityUiState.MicEnabled);
        Assert.Contains("download the Whisper speech model", capabilityUiState.MicToolTip, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("tool calling", capabilityUiState.ProviderToolTip, StringComparison.OrdinalIgnoreCase);
        ProviderAvailabilityTracker instance = ProviderAvailabilityTracker.Instance;
        instance.SetCooldown(123, CooldownReason.RateLimit, "Rate limit reached");
        try
        {
            ProviderUiStateApplicationService.AvailabilityUiState availabilityUiState = providerUiStateApplicationService.BuildAvailabilityUiState(123, instance);
            Assert.True(availabilityUiState.IsOnCooldown);
            Assert.Contains("Rate limit reached", availabilityUiState.CooldownMessage, StringComparison.Ordinal);
            Assert.Contains("Click", availabilityUiState.CheckAgainToolTip, StringComparison.Ordinal);
        }
        finally
        {
            instance.ClearCooldown(123);
        }
        TokenUsage usage = new TokenUsage
        {
            Used = 1000L,
            Limit = 1000L,
            Unit = "tokens"
        };
        ProviderUiStateApplicationService.TokenUsageUiState tokenUsageUiState = providerUiStateApplicationService.BuildTokenUsageUiState(usage, "StubProvider", limitNotificationShown: false, limitBubbleShown: false);
        Assert.Contains("Token Usage:", tokenUsageUiState.InputToolTip, StringComparison.Ordinal);
        Assert.True(tokenUsageUiState.ShouldShowLimitNotification);
        Assert.True(tokenUsageUiState.ShouldShowLimitBubble);
    }

    [Fact]
    public void ProviderUiStateApplicationService_ResetsNotifications_AndHandlesMissingMic()
    {
        ProviderUiStateApplicationService providerUiStateApplicationService = new ProviderUiStateApplicationService();
        ProviderUiStateApplicationService.CapabilityUiState capabilityUiState = providerUiStateApplicationService.BuildCapabilityUiState(null, speechHasMic: false, speechModelExists: false, "Mic is disabled");
        Assert.False(capabilityUiState.CanImages);
        Assert.False(capabilityUiState.MicEnabled);
        Assert.Equal("Mic is disabled", capabilityUiState.MicToolTip);
        ProviderUiStateApplicationService.AvailabilityUiState availabilityUiState = providerUiStateApplicationService.BuildAvailabilityUiState(null, ProviderAvailabilityTracker.Instance);
        Assert.False(availabilityUiState.IsOnCooldown);
        Assert.Null(availabilityUiState.CooldownMessage);
        Assert.Contains("Check", availabilityUiState.CheckAgainToolTip, StringComparison.OrdinalIgnoreCase);
        TokenUsage usage = new TokenUsage
        {
            Used = 1250L,
            Limit = 5000L,
            Unit = "USD",
            ResetDate = new DateTime(2026, 3, 29, 9, 30, 0, DateTimeKind.Local)
        };
        ProviderUiStateApplicationService.TokenUsageUiState tokenUsageUiState = providerUiStateApplicationService.BuildTokenUsageUiState(usage, "StubProvider", limitNotificationShown: true, limitBubbleShown: true);
        Assert.Contains("USD /", tokenUsageUiState.InputToolTip, StringComparison.Ordinal);
        Assert.Contains("Remaining:", tokenUsageUiState.InputToolTip, StringComparison.Ordinal);
        Assert.Contains("Reset on 2026-03-29 09:30", tokenUsageUiState.InputToolTip, StringComparison.Ordinal);
        Assert.False(tokenUsageUiState.ShouldShowLimitNotification);
        Assert.False(tokenUsageUiState.ShouldShowLimitBubble);
        Assert.True(tokenUsageUiState.ResetLimitNotification);
        Assert.True(tokenUsageUiState.ResetLimitBubble);
    }

    [Fact]
    public async Task ProviderModelCatalogApplicationService_UsesLiveModelsWhenAvailable()
    {
        ProviderModelCatalogApplicationService service = new ProviderModelCatalogApplicationService();
        TestMetadataWithLiveModels metadata = new TestMetadataWithLiveModels("OpenAI", new List<ModelDefinition>(1)
        {
            new ModelDefinition
            {
                Id = "default-a",
                DisplayName = "Default A"
            }
        }, new List<ModelDefinition>(2)
        {
            new ModelDefinition
            {
                Id = "live-a",
                DisplayName = "Live A"
            },
            new ModelDefinition
            {
                Id = "live-b",
                DisplayName = "Live B"
            }
        });
        ProviderModelCatalogApplicationService.ProviderModelCatalogResult liveCatalog = await service.LoadModelsAsync(metadata, "sk-test-key", "https://api.example.com");
        Assert.True(liveCatalog.UsedLiveModels);
        Assert.Equal(2, liveCatalog.EffectiveModels.Count);
        Assert.Contains("fetched", liveCatalog.StatusMessage, StringComparison.OrdinalIgnoreCase);
        ProviderModelCatalogApplicationService.ProviderModelCatalogResult defaultCatalog = await service.LoadModelsAsync(metadata, null, null);
        Assert.False(defaultCatalog.UsedLiveModels);
        Assert.Single(defaultCatalog.EffectiveModels);
        Assert.Null(defaultCatalog.StatusMessage);
    }

    [Fact]
    public async Task ProviderModelCatalogApplicationService_FallsBackForEmptyLiveResults_AndRethrowsCancellation()
    {
        ProviderModelCatalogApplicationService service = new ProviderModelCatalogApplicationService();
        List<ModelDefinition> defaultModels = new List<ModelDefinition>
        {
            new ModelDefinition
            {
                Id = "default-a",
                DisplayName = "Default A"
            }
        };
        DelegatingMetadata emptyMetadata = new DelegatingMetadata("OpenAI", defaultModels, (string? _, string? _, CancellationToken _) => Task.FromResult(new List<ModelDefinition>()));
        ProviderModelCatalogApplicationService.ProviderModelCatalogResult emptyResult = await service.LoadModelsAsync(emptyMetadata, "sk-test-key", "https://api.example.com");
        Assert.False(emptyResult.UsedLiveModels);
        Assert.Single(emptyResult.EffectiveModels);
        Assert.Contains("built-in list", emptyResult.StatusMessage, StringComparison.OrdinalIgnoreCase);
        DelegatingMetadata throwingMetadata = new DelegatingMetadata("OpenAI", defaultModels, delegate
        {
            throw new InvalidOperationException("boom");
        });
        ProviderModelCatalogApplicationService.ProviderModelCatalogResult fallbackResult = await service.LoadModelsAsync(throwingMetadata, "sk-test-key", "https://api.example.com");
        Assert.False(fallbackResult.UsedLiveModels);
        Assert.Single(fallbackResult.EffectiveModels);
        Assert.Contains("built-in list", fallbackResult.StatusMessage, StringComparison.OrdinalIgnoreCase);
        DelegatingMetadata canceledMetadata = new DelegatingMetadata("OpenAI", defaultModels, delegate
        {
            throw new OperationCanceledException();
        });
        await Assert.ThrowsAsync<OperationCanceledException>(() => service.LoadModelsAsync(canceledMetadata, "sk-test-key", "https://api.example.com"));
    }

    [Fact]
    public void OllamaModelCatalogApplicationService_BuildsInstalledAndRecommendedCatalog()
    {
        OllamaModelCatalogApplicationService ollamaModelCatalogApplicationService = new OllamaModelCatalogApplicationService();
        OllamaService.OllamaModel[] installed = new OllamaService.OllamaModel[1]
        {
            new OllamaService.OllamaModel
            {
                Name = "llama3.2:3b",
                Size = 3221225472L,
                Digest = "a"
            }
        };
        OllamaService.OllamaModel[] available = new OllamaService.OllamaModel[2]
        {
            new OllamaService.OllamaModel
            {
                Name = "llama3.2:3b",
                Size = 3221225472L,
                Digest = "a"
            },
            new OllamaService.OllamaModel
            {
                Name = "qwen2.5-coder:7b",
                Size = 7516192768L,
                Digest = "b"
            }
        };
        OllamaService.OllamaSystemProfile profile = new OllamaService.OllamaSystemProfile(32.0, 200.0, 8.0, "Test GPU", "strong", "Test profile");
        IReadOnlyList<OllamaCatalogItem> readOnlyList = ollamaModelCatalogApplicationService.BuildCatalog(installed, available, profile);
        Assert.Equal(2, readOnlyList.Count);
        Assert.True(readOnlyList[0].DisplayName.Contains("✓") || readOnlyList[0].DisplayName.Contains("installed"), "Expected installed indicator");
        Assert.Equal("llama3.2:3b", readOnlyList[0].ModelName);
        Assert.True(readOnlyList[0].IsInstalled);
        Assert.EndsWith(" GB", readOnlyList[0].SizeText, StringComparison.Ordinal);
        Assert.Equal("qwen2.5-coder:7b", readOnlyList[1].ModelName);
        Assert.False(readOnlyList[1].IsInstalled);
    }

    [Fact]
    public void OllamaModelCatalogApplicationService_FormatsModelSizes()
    {
        Assert.Equal(string.Empty, OllamaModelCatalogApplicationService.FormatModelSize(0L));
        Assert.Equal("512 MB", OllamaModelCatalogApplicationService.FormatModelSize(536870912L));
        Assert.EndsWith(" GB", OllamaModelCatalogApplicationService.FormatModelSize(2147483648L), StringComparison.Ordinal);
    }

    [Fact]
    public void OnboardingOllamaApplicationService_BuildsHardwareGuidance_AndCatalog()
    {
        OnboardingOllamaApplicationService onboardingOllamaApplicationService = new OnboardingOllamaApplicationService();
        OllamaService.OllamaSystemProfile profile = new OllamaService.OllamaSystemProfile(16.0, 120.0, 8.0, "Test GPU", "balanced", "test");
        OllamaHardwareGuidance ollamaHardwareGuidance = onboardingOllamaApplicationService.BuildHardwareGuidance(profile);
        Assert.Contains("16 GB RAM", ollamaHardwareGuidance.SummaryLine, StringComparison.Ordinal);
        Assert.Contains("8 GB VRAM", ollamaHardwareGuidance.SummaryLine, StringComparison.Ordinal);
        Assert.Null(ollamaHardwareGuidance.WarningLine);
        OllamaService.OllamaModel[] installed = new OllamaService.OllamaModel[1]
        {
            new OllamaService.OllamaModel
            {
                Name = "llama3.2:3b",
                Size = 3221225472L
            }
        };
        OllamaService.OllamaModel[] available = new OllamaService.OllamaModel[2]
        {
            new OllamaService.OllamaModel
            {
                Name = "llama3.2:3b",
                Size = 3221225472L
            },
            new OllamaService.OllamaModel
            {
                Name = "qwen2.5-coder:7b",
                Size = 7516192768L
            }
        };
        OnboardingOllamaCatalog onboardingOllamaCatalog = onboardingOllamaApplicationService.BuildCatalog(installed, available, profile);
        Assert.Contains("1 model installed", onboardingOllamaCatalog.ReadyText, StringComparison.Ordinal);
        Assert.Contains("Type to filter", onboardingOllamaCatalog.HintText, StringComparison.Ordinal);
        Assert.Equal("llama3.2:3b", onboardingOllamaCatalog.Entries[0].ModelName);
        Assert.True(onboardingOllamaCatalog.Entries[0].IsInstalled);
    }

    [Fact]
    public void OnboardingOllamaApplicationService_WarnsForVeryLowRam()
    {
        OnboardingOllamaApplicationService onboardingOllamaApplicationService = new OnboardingOllamaApplicationService();
        OllamaService.OllamaSystemProfile profile = new OllamaService.OllamaSystemProfile(2.0, 20.0, 0.0, string.Empty, "starter", "test");
        OllamaHardwareGuidance ollamaHardwareGuidance = onboardingOllamaApplicationService.BuildHardwareGuidance(profile);
        Assert.NotNull(ollamaHardwareGuidance.WarningLine);
        Assert.Contains("Only", ollamaHardwareGuidance.WarningLine, StringComparison.Ordinal);
        Assert.Contains("GB RAM detected", ollamaHardwareGuidance.WarningLine, StringComparison.Ordinal);
    }

    [Fact]
    public void OnboardingOllamaApplicationService_HandlesUnknownRam_AndNoInstalledModels()
    {
        OnboardingOllamaApplicationService onboardingOllamaApplicationService = new OnboardingOllamaApplicationService();
        OllamaService.OllamaSystemProfile profile = new OllamaService.OllamaSystemProfile(0.0, 100.0, 0.0, string.Empty, "unknown", "test");
        OllamaHardwareGuidance ollamaHardwareGuidance = onboardingOllamaApplicationService.BuildHardwareGuidance(profile);
        Assert.Equal("Could not detect RAM. Most models need at least 4 GB.", ollamaHardwareGuidance.SummaryLine);
        Assert.Null(ollamaHardwareGuidance.WarningLine);
        OnboardingOllamaCatalog onboardingOllamaCatalog = onboardingOllamaApplicationService.BuildCatalog(Array.Empty<OllamaService.OllamaModel>(), Array.Empty<OllamaService.OllamaModel>(), profile);
        Assert.Contains("No models installed yet", onboardingOllamaCatalog.HintText, StringComparison.Ordinal);
        Assert.Contains("running on your machine", onboardingOllamaCatalog.ReadyText, StringComparison.Ordinal);
        Assert.Empty(onboardingOllamaCatalog.Entries);
    }

    [Fact]
    public async Task OllamaActionApplicationService_NormalizesInstallDownloadAndUninstallResults()
    {
        FakeOllamaManagementClient client = new FakeOllamaManagementClient();
        OllamaActionApplicationService service = new OllamaActionApplicationService(client);
        OllamaActionResult install = await service.InstallAsync();
        Assert.True(install.Succeeded);
        Assert.Contains("installed", install.UserMessage, StringComparison.OrdinalIgnoreCase);
        OllamaActionResult download = await service.DownloadModelAsync("qwen2.5-coder:7b", "http://localhost:11434");
        Assert.True(download.Succeeded);
        Assert.Contains("qwen2.5-coder:7b", download.UserMessage, StringComparison.Ordinal);
        Assert.Equal("qwen2.5-coder:7b", client.LastPulledModel);
        Assert.Equal("http://localhost:11434", client.LastBaseUrl);
        OllamaActionResult uninstall = await service.UninstallModelAsync("qwen2.5-coder:7b", "http://localhost:11434");
        Assert.True(uninstall.Succeeded);
        Assert.Contains("uninstalled", uninstall.UserMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("qwen2.5-coder:7b", client.LastDeletedModel);
    }

    [Fact]
    public async Task OllamaActionApplicationService_ReturnsFailureMessages_FromClientErrors()
    {
        FakeOllamaManagementClient client = new FakeOllamaManagementClient
        {
            InstallException = new InvalidOperationException("install failed"),
            PullException = new InvalidOperationException("pull failed"),
            DeleteException = new InvalidOperationException("delete failed")
        };
        OllamaActionApplicationService service = new OllamaActionApplicationService(client);
        OllamaActionResult install = await service.InstallAsync();
        Assert.False(install.Succeeded);
        Assert.Equal("Could not install Ollama.", install.UserMessage);
        OllamaActionResult download = await service.DownloadModelAsync("broken");
        Assert.False(download.Succeeded);
        Assert.Equal("Could not download model.", download.UserMessage);
        OllamaActionResult uninstall = await service.UninstallModelAsync("broken");
        Assert.False(uninstall.Succeeded);
        Assert.Equal("Could not uninstall model.", uninstall.UserMessage);
    }

    [Fact]
    public async Task CodexActionApplicationService_NormalizesInstallResults()
    {
        FakeCodexManagementClient client = new FakeCodexManagementClient();
        CodexActionApplicationService service = new CodexActionApplicationService(client);
        List<string> updates = new List<string>();
        CodexActionResult result = await service.InstallAsync(new Progress<string>(delegate (string message)
        {
            updates.Add(message);
        }));
        Assert.True(result.Succeeded);
        Assert.Contains("installed", result.UserMessage, StringComparison.OrdinalIgnoreCase);
        Assert.True(client.InstallCalled);
        Assert.NotEmpty(updates);
    }

    [Fact]
    public async Task CodexActionApplicationService_ReturnsFailureMessages()
    {
        FakeCodexManagementClient client = new FakeCodexManagementClient
        {
            InstallException = new InvalidOperationException("npm failed")
        };
        CodexActionApplicationService service = new CodexActionApplicationService(client);
        CodexActionResult result = await service.InstallAsync();
        Assert.False(result.Succeeded);
        Assert.Equal("Could not install Codex CLI.", result.UserMessage);
    }

    [Fact]
    public async Task ToolCategorySettingsApplicationService_LoadsDefaults_AndPersistsNormalizedCategories()
    {
        InMemorySettingsRepository repository = new InMemorySettingsRepository();
        ToolCategorySettingsApplicationService service = new ToolCategorySettingsApplicationService(repository);

        ToolCategorySelection defaults = await service.LoadAsync();
        Assert.True(defaults.ToolsEnabled);
        Assert.Contains("filesystem", defaults.EnabledCategories, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("browser", defaults.EnabledCategories, StringComparer.OrdinalIgnoreCase);

        await service.SaveAsync(new[] { "browser", "filesystem", "browser", "unknown-category" });
        ToolCategorySelection saved = await service.LoadAsync();
        Assert.Equal(new[] { "browser", "filesystem" }, saved.EnabledCategories.OrderBy(x => x).ToArray());

        await service.SaveAsync(Array.Empty<string>());
        ToolCategorySelection disabled = await service.LoadAsync();
        Assert.False(disabled.ToolsEnabled);
        Assert.Empty(disabled.EnabledCategories);
    }

    private static ToolCallRequest CreateRequest(string tool, string json)
    {
        return new ToolCallRequest
        {
            Tool = tool,
            Parameters = JsonDocument.Parse(json).RootElement.Clone()
        };
    }
}
