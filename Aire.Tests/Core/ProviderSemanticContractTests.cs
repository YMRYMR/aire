extern alias AireCore;
extern alias AireWpf;

using System;
using System.Text.Json;
using System.Threading;
using AireCore::Aire.Domain.Providers;
using AireCore::Aire.Providers;
using AireWpf::Aire.AppLayer.Abstractions;
using Xunit;

namespace Aire.Tests.Core;

public class ProviderSemanticContractTests
{
    // ── WorkflowIntent ─────────────────────────────────────────────────────

    [Fact]
    public void WorkflowIntent_AssistantText_SetsKindAndDisplayText()
    {
        var intent = WorkflowIntent.AssistantText("Hello, world!");

        Assert.Equal(WorkflowIntentKind.AssistantText, intent.Kind);
        Assert.Equal("Hello, world!", intent.DisplayText);
        Assert.Null(intent.Tool);
        Assert.Null(intent.Parameters);
        Assert.Null(intent.ErrorMessage);
    }

    [Fact]
    public void WorkflowIntent_ToolCall_SetsKindToolAndDisplayText()
    {
        var tool = new ToolIntent { ToolName = "read_file", Description = "Read a file" };
        var intent = WorkflowIntent.ToolCall("I'll read that file.", tool);

        Assert.Equal(WorkflowIntentKind.ExecuteTool, intent.Kind);
        Assert.Equal("I'll read that file.", intent.DisplayText);
        Assert.Same(tool, intent.Tool);
    }

    [Theory]
    [InlineData(WorkflowIntentKind.SwitchModel)]
    [InlineData(WorkflowIntentKind.UpdateTodoList)]
    [InlineData(WorkflowIntentKind.AttemptCompletion)]
    [InlineData(WorkflowIntentKind.FollowUpQuestion)]
    public void WorkflowIntent_SemanticFactoryMethods_SetCorrectKind(
        WorkflowIntentKind expectedKind)
    {
        const string parametersJson = """{"value":"test"}""";

        var intent = expectedKind switch
        {
            WorkflowIntentKind.SwitchModel       => WorkflowIntent.SwitchModel("text", parametersJson),
            WorkflowIntentKind.UpdateTodoList    => WorkflowIntent.UpdateTodoList("text", parametersJson),
            WorkflowIntentKind.AttemptCompletion => WorkflowIntent.AttemptCompletion("text", parametersJson),
            WorkflowIntentKind.FollowUpQuestion  => WorkflowIntent.FollowUpQuestion("text", parametersJson),
            _ => throw new ArgumentOutOfRangeException()
        };

        Assert.Equal(expectedKind, intent.Kind);
        Assert.Null(intent.Tool);
        Assert.True(intent.Parameters.HasValue);
        Assert.Equal("test", intent.Parameters.Value.GetProperty("value").GetString());
    }

    [Fact]
    public void WorkflowIntent_Error_SetsKindAndErrorMessage()
    {
        var intent = WorkflowIntent.Error("API key expired");

        Assert.Equal(WorkflowIntentKind.Error, intent.Kind);
        Assert.Equal("API key expired", intent.ErrorMessage);
        Assert.Null(intent.Tool);
        Assert.Null(intent.Parameters);
    }

    [Fact]
    public void WorkflowIntent_Canceled_SetsKindOnly()
    {
        var intent = WorkflowIntent.Canceled();

        Assert.Equal(WorkflowIntentKind.Canceled, intent.Kind);
        Assert.Equal(string.Empty, intent.DisplayText);
    }

    // ── ToolIntent ──────────────────────────────────────────────────────────

    [Fact]
    public void ToolIntent_Create_ParsesParameters()
    {
        var intent = ToolIntent.Create(
            "execute_command",
            """{"command":"ls","working_directory":"/tmp"}""",
            "Run ls in /tmp",
            """{"command":"ls","working_directory":"/tmp"}""");

        Assert.Equal("execute_command", intent.ToolName);
        Assert.Equal("Run ls in /tmp", intent.Description);
        Assert.Equal("ls", intent.Parameters.GetProperty("command").GetString());
        Assert.Equal("/tmp", intent.Parameters.GetProperty("working_directory").GetString());
        Assert.Equal("""{"command":"ls","working_directory":"/tmp"}""", intent.RawPayload);
    }

    [Fact]
    public void ToolIntent_Create_WithNullRawPayload_SetsNull()
    {
        var intent = ToolIntent.Create("read_file", "{}", "Read file");

        Assert.Null(intent.RawPayload);
    }

    [Fact]
    public void ToolIntent_ObjectInitializer_AllowsManualConstruction()
    {
        using var doc = JsonDocument.Parse("{}");
        var intent = new ToolIntent
        {
            ToolName = "custom_tool",
            Parameters = doc.RootElement.Clone(),
            Description = "Custom",
            RawPayload = null
        };

        Assert.Equal("custom_tool", intent.ToolName);
    }

    // ── ProviderRequestContext ──────────────────────────────────────────────

    [Fact]
    public void ProviderRequestContext_DefaultsAreSafe()
    {
        var ctx = new ProviderRequestContext();

        Assert.Empty(ctx.Messages);
        Assert.Equal(string.Empty, ctx.Model);
        Assert.Null(ctx.EnabledToolCategories);
        Assert.Null(ctx.SystemPrompt);
        Assert.Equal(0.0, ctx.Temperature);
        Assert.Equal(0, ctx.MaxTokens);
        Assert.Equal(CancellationToken.None, ctx.CancellationToken);
    }

    [Fact]
    public void ProviderRequestContext_AcceptsInitializedValues()
    {
        var messages = new[]
        {
            new ProviderRequestMessage { Role = "user", Content = "Hello" }
        };
        using var cts = new CancellationTokenSource();

        var ctx = new ProviderRequestContext
        {
            Messages = messages,
            Model = "glm-4-flash",
            EnabledToolCategories = new[] { "filesystem" },
            SystemPrompt = "You are helpful.",
            Temperature = 0.5,
            MaxTokens = 4096,
            CancellationToken = cts.Token
        };

        Assert.Single(ctx.Messages);
        Assert.Equal("glm-4-flash", ctx.Model);
        Assert.Single(ctx.EnabledToolCategories);
        Assert.Equal("You are helpful.", ctx.SystemPrompt);
    }

    // ── ProviderExecutionResult ────────────────────────────────────────────

    [Fact]
    public void ProviderExecutionResult_Succeeded_SetsFields()
    {
        var intent = WorkflowIntent.AssistantText("Hi");
        var result = ProviderExecutionResult.Succeeded(
            intent, "Hi", tokensUsed: 42, duration: TimeSpan.FromMilliseconds(200));

        Assert.True(result.IsSuccess);
        Assert.Same(intent, result.Intent);
        Assert.Equal("Hi", result.RawContent);
        Assert.Equal(42, result.TokensUsed);
        Assert.Equal(TimeSpan.FromMilliseconds(200), result.Duration);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void ProviderExecutionResult_Failed_SetsErrorFields()
    {
        var result = ProviderExecutionResult.Failed("Timeout", TimeSpan.FromSeconds(5));

        Assert.False(result.IsSuccess);
        Assert.Equal("Timeout", result.ErrorMessage);
        Assert.Equal(TimeSpan.FromSeconds(5), result.Duration);
        Assert.Null(result.Intent);
    }

    // ── ProviderValidationOutcome ──────────────────────────────────────────

    [Fact]
    public void ProviderValidationOutcome_Valid_ReturnsSuccess()
    {
        var outcome = ProviderValidationOutcome.Valid();

        Assert.True(outcome.IsValid);
        Assert.Null(outcome.ErrorMessage);
        Assert.Equal(ProviderValidationFailureKind.None, outcome.FailureKind);
        Assert.Null(outcome.RemediationHint);
    }

    [Theory]
    [InlineData(ProviderValidationFailureKind.InvalidCredentials)]
    [InlineData(ProviderValidationFailureKind.NetworkError)]
    [InlineData(ProviderValidationFailureKind.RateLimit)]
    [InlineData(ProviderValidationFailureKind.BillingError)]
    [InlineData(ProviderValidationFailureKind.ServiceUnavailable)]
    [InlineData(ProviderValidationFailureKind.Unknown)]
    public void ProviderValidationOutcome_Invalid_SetsFailureKind(ProviderValidationFailureKind kind)
    {
        var outcome = ProviderValidationOutcome.Invalid("Bad config", kind, "Check settings");

        Assert.False(outcome.IsValid);
        Assert.Equal("Bad config", outcome.ErrorMessage);
        Assert.Equal(kind, outcome.FailureKind);
        Assert.Equal("Check settings", outcome.RemediationHint);
    }

    [Fact]
    public void ProviderValidationOutcome_Invalid_WithoutHint_HasNullRemediation()
    {
        var outcome = ProviderValidationOutcome.Invalid("Error");

        Assert.Null(outcome.RemediationHint);
        Assert.Equal(ProviderValidationFailureKind.Unknown, outcome.FailureKind);
    }
}
