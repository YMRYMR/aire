using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Aire.Providers;
using Aire.Services;
using Xunit;

namespace Aire.Tests.Services;

public class CoreServiceExtractionTests
{
    private sealed class FakeAiProvider : BaseAiProvider
    {
        public override string ProviderType => "Fake";
        public override string DisplayName  => "Fake";

        protected override ProviderCapabilities GetBaseCapabilities()
            => ProviderCapabilities.TextChat | ProviderCapabilities.Streaming;

        public override Task<AiResponse> SendChatAsync(
            IEnumerable<ChatMessage> messages,
            CancellationToken cancellationToken = default)
        {
            var text = "";
            foreach (var message in messages)
                text = message.Content;
            return Task.FromResult(new AiResponse { IsSuccess = true, Content = "echo:" + text });
        }

        public override async IAsyncEnumerable<string> StreamChatAsync(
            IEnumerable<ChatMessage> messages,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var text = "";
            foreach (var message in messages)
                text = message.Content;
            yield return "echo:";
            yield return text;
        }
    }

    [Fact]
    public void ProviderErrorClassifier_ClassifiesRateLimitHttpErrors()
    {
        HttpRequestException ex = new HttpRequestException("rate limited", null, HttpStatusCode.TooManyRequests);
        string message;
        CooldownReason actual = ProviderErrorClassifier.Classify(ex, out message);
        Assert.Equal(CooldownReason.RateLimit, actual);
        Assert.Contains("Cooling down", message);
    }

    [Fact]
    public void ProviderAvailabilityTracker_ExpiresCooldowns()
    {
        ProviderAvailabilityTracker instance = ProviderAvailabilityTracker.Instance;
        instance.ClearCooldown(999999);
        instance.SetCooldown(999999, CooldownReason.RateLimit, "rate limited");
        Assert.True(instance.IsOnCooldown(999999));
        Assert.NotNull(instance.GetCooldown(999999));
        instance.ClearCooldown(999999);
        Assert.False(instance.IsOnCooldown(999999));
        Assert.Null(instance.GetCooldown(999999));
    }

    [Fact]
    public async Task ChatOrchestrator_SendMessageAsync_UsesSelectedProvider()
    {
        ChatOrchestrator orchestrator = new ChatOrchestrator();
        FakeAiProvider provider = new FakeAiProvider();
        orchestrator.SetProvider(provider);
        AiResponse response = await orchestrator.SendMessageAsync("hello");
        Assert.True(response.IsSuccess);
        Assert.Equal("echo:hello", response.Content);
    }

    [Fact]
    public async Task ChatOrchestrator_StreamMessageAsync_RaisesChunkEvents()
    {
        ChatOrchestrator orchestrator = new ChatOrchestrator();
        FakeAiProvider provider = new FakeAiProvider();
        orchestrator.SetProvider(provider);
        List<string> chunks = new List<string>();
        orchestrator.ResponseChunkReceived += delegate (object? _, string chunk)
        {
            chunks.Add(chunk);
        };
        await orchestrator.StreamMessageAsync("hello");
        Assert.Equal(new string[] { "echo:", "hello" }, chunks);
    }

    [Fact]
    public void ToolExecutionMetadata_NormalizesKnownAliases()
    {
        Assert.Equal("read_browser_tab", ToolExecutionMetadata.NormalizeToolName("read_webbrowser_tabs"));
        Assert.Equal("show_notification", ToolExecutionMetadata.NormalizeToolName("notify"));
        Assert.Equal("show_image", ToolExecutionMetadata.NormalizeToolName("display_image"));
    }

    [Fact]
    public void ToolExecutionMetadata_ClassifiesSessionTools()
    {
        Assert.True(ToolExecutionMetadata.IsKeyboardTool("type_text"));
        Assert.True(ToolExecutionMetadata.IsMouseTool("mouse_click"));
        Assert.True(ToolExecutionMetadata.IsSessionTool("take_screenshot"));
        Assert.False(ToolExecutionMetadata.IsSessionTool("open_url"));
    }

    [Fact]
    public void BuiltinToolSkillService_ListTools_ReturnsCatalog()
    {
        using JsonDocument jsonDocument = JsonDocument.Parse("{\"name\":\"list_tools\"}");
        ToolCallRequest request = new ToolCallRequest
        {
            Tool = "skill",
            Parameters = jsonDocument.RootElement.Clone()
        };
        ToolExecutionResult toolExecutionResult = BuiltinToolSkillService.Execute(request);
        Assert.Contains("AVAILABLE TOOLS", toolExecutionResult.TextResult);
        Assert.Contains("execute_command", toolExecutionResult.TextResult);
    }
}
