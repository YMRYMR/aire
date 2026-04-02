using System;
using System.Text.Json;
using Aire.AppLayer.Providers;
using Aire.Domain.Providers;
using Aire.Providers;
using Xunit;

namespace Aire.Tests.Services;

public class ProviderExecutionResultMapperTests
{
    [Fact]
    public void FromLegacyResponse_MapsAssistantText()
    {
        AiResponse response = new()
        {
            IsSuccess = true,
            Content = "Hello",
            TokensUsed = 12,
            Duration = TimeSpan.FromSeconds(1)
        };

        var result = ProviderExecutionResultMapper.FromLegacyResponse(response);

        Assert.True(result.IsSuccess);
        Assert.Equal("Hello", result.RawContent);
        Assert.Equal(12, result.TokensUsed);
        Assert.Equal(WorkflowIntentKind.AssistantText, result.Intent?.Kind);
        Assert.Equal("Hello", result.Intent?.DisplayText);
    }

    [Fact]
    public void FromLegacyResponse_MapsExecuteToolIntent()
    {
        AiResponse response = new()
        {
            IsSuccess = true,
            Content = "<tool_call>{\"tool\":\"read_file\",\"path\":\"C:/Temp/test.txt\"}</tool_call>"
        };

        var result = ProviderExecutionResultMapper.FromLegacyResponse(response);

        Assert.True(result.IsSuccess);
        Assert.Equal(WorkflowIntentKind.ExecuteTool, result.Intent?.Kind);
        Assert.Equal("read_file", result.Intent?.Tool?.ToolName);
        Assert.Equal("C:/Temp/test.txt", result.Intent?.Tool?.Parameters.GetProperty("path").GetString());
    }

    [Theory]
    [InlineData("<tool_call>{\"tool\":\"switch_model\",\"model_name\":\"gpt-5\"}</tool_call>", WorkflowIntentKind.SwitchModel, "model_name", "gpt-5")]
    [InlineData("<tool_call>{\"tool\":\"ask_followup_question\",\"question\":\"Need more info?\"}</tool_call>", WorkflowIntentKind.FollowUpQuestion, "question", "Need more info?")]
    [InlineData("<tool_call>{\"tool\":\"attempt_completion\",\"result\":\"done\"}</tool_call>", WorkflowIntentKind.AttemptCompletion, "result", "done")]
    public void FromLegacyResponse_MapsWorkflowSemanticIntents(string content, WorkflowIntentKind expectedKind, string propertyName, string expectedValue)
    {
        AiResponse response = new()
        {
            IsSuccess = true,
            Content = content
        };

        var result = ProviderExecutionResultMapper.FromLegacyResponse(response);

        Assert.Equal(expectedKind, result.Intent?.Kind);
        Assert.Null(result.Intent?.Tool);
        Assert.True(result.Intent?.Parameters.HasValue);
        Assert.Equal(expectedValue, result.Intent?.Parameters!.Value.GetProperty(propertyName).GetString());
    }

    [Fact]
    public void FromLegacyResponse_MapsFailure()
    {
        AiResponse response = new()
        {
            IsSuccess = false,
            ErrorMessage = "boom",
            Duration = TimeSpan.FromSeconds(2)
        };

        var result = ProviderExecutionResultMapper.FromLegacyResponse(response);

        Assert.False(result.IsSuccess);
        Assert.Equal("boom", result.ErrorMessage);
        Assert.Equal(TimeSpan.FromSeconds(2), result.Duration);
        Assert.Null(result.Intent);
    }
}
