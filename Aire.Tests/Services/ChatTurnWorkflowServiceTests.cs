using System;
using System.Collections.Generic;
using Aire.Domain.Providers;
using Aire.Providers;
using Aire.Services;
using Aire.Services.Mcp;
using Aire.Services.Workflows;
using Xunit;

namespace Aire.Tests.Services;

public sealed class ChatTurnWorkflowServiceTests
{
    [Fact]
    public void BuildRequestMessages_AddsSystemPromptAndMcpSection_WhenToolsEnabled()
    {
        var service = new ChatTurnWorkflowService();
        var provider = new FakeProvider
        {
            ToolOutputFormatValue = ToolOutputFormat.AireText,
            CapabilitiesValue = ProviderCapabilities.TextChat | ProviderCapabilities.ToolCalling | ProviderCapabilities.SystemPrompt
        };
        var history = new List<ChatMessage>
        {
            new() { Role = "user", Content = "Hello" }
        };

        var messages = service.BuildRequestMessages(
            provider,
            "\nMODELS:\n- gpt-5.4-mini",
            history,
            [
                new McpToolDefinition
                {
                    Name = "search_docs",
                    Description = "Search docs",
                    ServerName = "docs"
                }
            ],
            toolsEnabled: true);

        Assert.Equal(2, messages.Count);
        Assert.Equal("system", messages[0].Role);
        Assert.Contains("AVAILABLE TOOLS", messages[0].Content, StringComparison.Ordinal);
        Assert.Contains("MODELS:", messages[0].Content, StringComparison.Ordinal);
        Assert.Contains("MCP TOOLS", messages[0].Content, StringComparison.Ordinal);
        Assert.Same(history[0], messages[1]);
    }

    [Fact]
    public void BuildRequestMessages_AppendsModePromptSection_WhenProvided()
    {
        var service = new ChatTurnWorkflowService();
        var provider = new FakeProvider
        {
            ToolOutputFormatValue = ToolOutputFormat.AireText,
            CapabilitiesValue = ProviderCapabilities.TextChat | ProviderCapabilities.ToolCalling | ProviderCapabilities.SystemPrompt
        };

        var messages = service.BuildRequestMessages(
            provider,
            string.Empty,
            [],
            toolsEnabled: true,
            modePromptSection: "\n\nCURRENT OPERATING MODE: Developer\nPrioritize concrete implementation details.");

        Assert.Single(messages);
        Assert.Contains("CURRENT OPERATING MODE: Developer", messages[0].Content, StringComparison.Ordinal);
        Assert.Contains("concrete implementation details", messages[0].Content, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildRequestMessages_OmitsSystemPrompt_WhenToolsDisabled()
    {
        var service = new ChatTurnWorkflowService();
        var provider = new FakeProvider
        {
            CapabilitiesValue = ProviderCapabilities.TextChat | ProviderCapabilities.ToolCalling | ProviderCapabilities.SystemPrompt
        };
        var history = new List<ChatMessage>
        {
            new() { Role = "user", Content = "Hello" }
        };

        var messages = service.BuildRequestMessages(provider, string.Empty, history, toolsEnabled: false);

        Assert.Single(messages);
        Assert.Same(history[0], messages[0]);
    }

    [Fact]
    public void BuildRequestMessages_UsesNativePrompt_ForNativeToolProviders()
    {
        var service = new ChatTurnWorkflowService();
        var provider = new FakeProvider
        {
            ToolOutputFormatValue = ToolOutputFormat.NativeToolCalls,
            CapabilitiesValue = ProviderCapabilities.TextChat | ProviderCapabilities.ToolCalling
        };

        var messages = service.BuildRequestMessages(provider, string.Empty, []);

        Assert.Single(messages);
        Assert.Equal("system", messages[0].Role);
        Assert.Contains("function calling", messages[0].Content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildRequestMessages_IncludesCapabilityQuestionRule_InSystemPrompt()
    {
        var service = new ChatTurnWorkflowService();
        var provider = new FakeProvider
        {
            ToolOutputFormatValue = ToolOutputFormat.AireText,
            CapabilitiesValue = ProviderCapabilities.TextChat | ProviderCapabilities.ToolCalling | ProviderCapabilities.SystemPrompt
        };

        var messages = service.BuildRequestMessages(
            provider,
            string.Empty,
            [new ChatMessage { Role = "user", Content = "Can you generate images?" }],
            toolsEnabled: true);

        Assert.Equal(2, messages.Count);
        Assert.Contains("capability or product question", messages[0].Content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("answer directly in plain language", messages[0].Content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Do NOT call tools", messages[0].Content, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Can you generate images?", messages[1].Content);
    }

    [Fact]
    public void BuildRequestMessages_UsesCompactPrompt_ForNativeProviderWithCompactEnabled()
    {
        var service = new ChatTurnWorkflowService();
        var provider = new FakeCompactProvider
        {
            ToolOutputFormatValue = ToolOutputFormat.NativeToolCalls,
            CapabilitiesValue = ProviderCapabilities.TextChat | ProviderCapabilities.ToolCalling
        };

        var messages = service.BuildRequestMessages(provider, string.Empty, []);

        Assert.Single(messages);
        Assert.Equal("system", messages[0].Role);
        // Compact prompt is much shorter — it does NOT contain tool listing sequences
        Assert.DoesNotContain("TOOL CALL SEQUENCES", messages[0].Content, StringComparison.Ordinal);
        // But must still contain core behavioral rules
        Assert.Contains("function calling", messages[0].Content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildFailureOutcome_ClassifiesCooldown()
    {
        var service = new ChatTurnWorkflowService();

        var outcome = service.BuildFailureOutcome("Rate limit exceeded.");

        Assert.Equal(ChatTurnWorkflowService.OutcomeKind.Error, outcome.Kind);
        Assert.Equal(CooldownReason.RateLimit, outcome.CooldownReason);
        Assert.NotNull(outcome.CooldownMessage);
    }

    [Fact]
    public void BuildErrorOutcome_MapsCanceledException()
    {
        var service = new ChatTurnWorkflowService();

        var outcome = service.BuildErrorOutcome(new OperationCanceledException("Canceled"));

        Assert.Equal(ChatTurnWorkflowService.OutcomeKind.Canceled, outcome.Kind);
        Assert.Equal("Canceled", outcome.ErrorMessage);
    }

    [Fact]
    public void ParseResponse_MapsAssistantText()
    {
        var service = new ChatTurnWorkflowService();

        var outcome = service.ParseResponse(new AiResponse
        {
            IsSuccess = true,
            Content = "Plain assistant response."
        });

        Assert.Equal(ChatTurnWorkflowService.OutcomeKind.SuccessText, outcome.Kind);
        Assert.Equal("Plain assistant response.", outcome.TextContent);
        Assert.NotNull(outcome.ParsedResponse);
        Assert.False(outcome.ParsedResponse!.HasToolCall);
    }

    [Fact]
    public void ParseResponse_MapsExecuteTool()
    {
        var service = new ChatTurnWorkflowService();

        var outcome = service.ParseResponse(new AiResponse
        {
            IsSuccess = true,
            Content = "<tool_call>{\"tool\":\"read_file\",\"path\":\"C:\\\\dev\\\\aire\\\\README.md\"}</tool_call>"
        });

        Assert.Equal(ChatTurnWorkflowService.OutcomeKind.ExecuteTool, outcome.Kind);
        Assert.NotNull(outcome.ParsedResponse);
        Assert.True(outcome.ParsedResponse!.HasToolCall);
        Assert.Equal("read_file", outcome.ParsedResponse.ToolCall!.Tool);
    }

    [Fact]
    public void ParseResponse_MapsWorkflowIntents()
    {
        var service = new ChatTurnWorkflowService();

        var switchModel = service.ParseResponse(new AiResponse
        {
            IsSuccess = true,
            Content = "<tool_call>{\"tool\":\"switch_model\",\"model_name\":\"gpt-5.4-mini\",\"reason\":\"lighter task\",\"direction\":\"down\"}</tool_call>"
        });
        var followUp = service.ParseResponse(new AiResponse
        {
            IsSuccess = true,
            Content = "<tool_call>{\"tool\":\"ask_followup_question\",\"question\":\"Which folder?\"}</tool_call>"
        });
        var attemptCompletion = service.ParseResponse(new AiResponse
        {
            IsSuccess = true,
            Content = "<tool_call>{\"tool\":\"attempt_completion\",\"result\":\"Done.\"}</tool_call>"
        });
        var updateTodo = service.ParseResponse(new AiResponse
        {
            IsSuccess = true,
            Content = "<tool_call>{\"tool\":\"update_todo_list\",\"todos\":[{\"content\":\"One\",\"status\":\"pending\",\"activeForm\":\"Doing one\"}]}</tool_call>"
        });

        Assert.Equal(ChatTurnWorkflowService.OutcomeKind.SwitchModel, switchModel.Kind);
        Assert.Equal(ChatTurnWorkflowService.OutcomeKind.AskFollowUpQuestion, followUp.Kind);
        Assert.Equal(ChatTurnWorkflowService.OutcomeKind.AttemptCompletion, attemptCompletion.Kind);
        Assert.Equal(ChatTurnWorkflowService.OutcomeKind.UpdateTodoList, updateTodo.Kind);
    }

    [Fact]
    public void ParseResponse_MapsFailureResponses_ToErrorOutcome()
    {
        var service = new ChatTurnWorkflowService();

        var outcome = service.ParseResponse(new AiResponse
        {
            IsSuccess = false,
            ErrorMessage = "Service unavailable."
        });

        Assert.Equal(ChatTurnWorkflowService.OutcomeKind.Error, outcome.Kind);
        Assert.Equal(CooldownReason.ServiceUnavailable, outcome.CooldownReason);
        Assert.Equal("Service unavailable.", outcome.ErrorMessage);
    }

    /// <summary>A fake provider that uses compact native prompts (simulates OpenAI/Anthropic/Gemini).</summary>
    private sealed class FakeCompactProvider : IAiProvider
    {
        public string ProviderType => "FakeCompact";
        public string DisplayName => "Fake Compact";
        public ProviderCapabilities CapabilitiesValue { get; init; } = ProviderCapabilities.TextChat;
        public ToolOutputFormat ToolOutputFormatValue { get; init; } = ToolOutputFormat.NativeToolCalls;

        public ProviderCapabilities Capabilities => CapabilitiesValue;
        public ToolCallMode ToolCallMode => ToolCallMode.NativeFunctionCalling;
        public ToolOutputFormat ToolOutputFormat => ToolOutputFormatValue;

        public bool Has(ProviderCapabilities cap) => (Capabilities & cap) == cap;
        public void Initialize(ProviderConfig config) { }
        public System.Threading.Tasks.Task<AiResponse> SendChatAsync(IEnumerable<ChatMessage> messages, System.Threading.CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public void PrepareForCapabilityTesting() { }
        public void SetToolsEnabled(bool enabled) { }
        public void SetEnabledToolCategories(IEnumerable<string>? categories) { }
        public async IAsyncEnumerable<string> StreamChatAsync(IEnumerable<ChatMessage> messages, [System.Runtime.CompilerServices.EnumeratorCancellation] System.Threading.CancellationToken cancellationToken = default)
        {
            await System.Threading.Tasks.Task.CompletedTask;
            yield break;
        }
        public System.Threading.Tasks.Task<ProviderValidationResult> ValidateConfigurationAsync(System.Threading.CancellationToken cancellationToken = default)
            => System.Threading.Tasks.Task.FromResult(ProviderValidationResult.Ok());
        public System.Threading.Tasks.Task<TokenUsage?> GetTokenUsageAsync(System.Threading.CancellationToken cancellationToken = default)
            => System.Threading.Tasks.Task.FromResult<TokenUsage?>(null);

        public string BuildToolSystemPrompt(string modelListSection, string? modePromptSection, string? mcpSection)
        {
            // Compact mode — simulates what OpenAI/Anthropic/Gemini providers do
            var basePrompt = Aire.Services.FileSystemSystemPrompt.BuildNativeCompact();
            var sb = new System.Text.StringBuilder(basePrompt);
            if (!string.IsNullOrEmpty(modelListSection)) sb.Append(modelListSection);
            if (!string.IsNullOrWhiteSpace(modePromptSection)) sb.Append(modePromptSection);
            if (!string.IsNullOrWhiteSpace(mcpSection)) sb.Append(mcpSection);
            return sb.ToString();
        }
    }

    private sealed class FakeProvider : IAiProvider
    {
        public string ProviderType => "Fake";
        public string DisplayName => "Fake";
        public ProviderCapabilities CapabilitiesValue { get; init; } = ProviderCapabilities.TextChat;
        public ToolOutputFormat ToolOutputFormatValue { get; init; } = ToolOutputFormat.AireText;

        public ProviderCapabilities Capabilities => CapabilitiesValue;
        public ToolCallMode ToolCallMode => ToolCallMode.TextBased;
        public ToolOutputFormat ToolOutputFormat => ToolOutputFormatValue;

        public bool Has(ProviderCapabilities cap) => (Capabilities & cap) == cap;
        public void Initialize(ProviderConfig config) { }
        public System.Threading.Tasks.Task<AiResponse> SendChatAsync(IEnumerable<ChatMessage> messages, System.Threading.CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public void PrepareForCapabilityTesting() { }
        public void SetToolsEnabled(bool enabled) { }
        public void SetEnabledToolCategories(IEnumerable<string>? categories) { }
        public async IAsyncEnumerable<string> StreamChatAsync(IEnumerable<ChatMessage> messages, [System.Runtime.CompilerServices.EnumeratorCancellation] System.Threading.CancellationToken cancellationToken = default)
        {
            await System.Threading.Tasks.Task.CompletedTask;
            yield break;
        }
        public System.Threading.Tasks.Task<ProviderValidationResult> ValidateConfigurationAsync(System.Threading.CancellationToken cancellationToken = default)
            => System.Threading.Tasks.Task.FromResult(ProviderValidationResult.Ok());
        public System.Threading.Tasks.Task<TokenUsage?> GetTokenUsageAsync(System.Threading.CancellationToken cancellationToken = default)
            => System.Threading.Tasks.Task.FromResult<TokenUsage?>(null);

        public string BuildToolSystemPrompt(string modelListSection, string? modePromptSection, string? mcpSection)
        {
            var basePrompt = ToolOutputFormat switch
            {
                ToolOutputFormat.Hermes          => Aire.Services.FileSystemSystemPrompt.HermesToolCallingText,
                ToolOutputFormat.React           => Aire.Services.FileSystemSystemPrompt.ReactToolCallingText,
                ToolOutputFormat.NativeToolCalls => Aire.Services.FileSystemSystemPrompt.NativeToolCallingText,
                _                                => Aire.Services.FileSystemSystemPrompt.Text,
            };
            var sb = new System.Text.StringBuilder(basePrompt);
            if (!string.IsNullOrEmpty(modelListSection)) sb.Append(modelListSection);
            if (!string.IsNullOrWhiteSpace(modePromptSection)) sb.Append(modePromptSection);
            if (!string.IsNullOrWhiteSpace(mcpSection)) sb.Append(mcpSection);
            return sb.ToString();
        }
    }
}
