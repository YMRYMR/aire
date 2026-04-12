using System.Text.Json;
using Aire.AppLayer.Chat;
using Aire.Services;
using Xunit;

namespace Aire.Tests.Services;

public sealed class ChatInteractionApplicationServiceTests
{
    private readonly ChatInteractionApplicationService _service = new();

    private static ToolCallRequest MakeToolCall(string json)
    {
        return new ToolCallRequest
        {
            Tool = "update_todo_list",
            Parameters = JsonDocument.Parse(json).RootElement
        };
    }

    [Fact]
    public void BuildTodoUpdate_ValidToolCall_ReturnsItemsAndStatusText()
    {
        // Arrange
        var toolCall = MakeToolCall(/*lang=json,strict*/
            """{"tasks":[{"id":"1","description":"Write tests","status":"completed"},{"id":"2","description":"Review PR","status":"pending"}]}""");

        // Act
        var result = _service.BuildTodoUpdate(toolCall);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Items.Count);

        Assert.Equal("1", result.Items[0].Id);
        Assert.Equal("Write tests", result.Items[0].Description);
        Assert.Equal("completed", result.Items[0].Status);

        Assert.Equal("2", result.Items[1].Id);
        Assert.Equal("Review PR", result.Items[1].Description);
        Assert.Equal("pending", result.Items[1].Status);

        Assert.Equal("Todo list updated: 2 task(s), 1 completed.", result.StatusText);
    }

    [Fact]
    public void BuildTodoUpdate_EmptyTaskList_ReturnsEmptyItemsAndStatusText()
    {
        // Arrange
        var toolCall = MakeToolCall(/*lang=json,strict*/
            """{"tasks":[]}""");

        // Act
        var result = _service.BuildTodoUpdate(toolCall);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result.Items);
        Assert.Equal("Todo list updated: 0 task(s), 0 completed.", result.StatusText);
    }

    [Fact]
    public void BuildFollowUpPrompt_ValidQuestionAndOptions_ReturnsPrompt()
    {
        // Arrange
        var toolCall = new ToolCallRequest
        {
            Tool = "ask_followup_question",
            Parameters = JsonDocument.Parse(/*lang=json,strict*/
                """{"question":"How should I proceed?","options":["Option A","Option B"]}""").RootElement
        };

        // Act
        var result = _service.BuildFollowUpPrompt(toolCall);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("How should I proceed?", result.Question);
        Assert.Equal(2, result.Options.Count);
        Assert.Equal("Option A", result.Options[0]);
        Assert.Equal("Option B", result.Options[1]);
        Assert.Equal("How should I proceed?", result.AssistantHistoryMessage);
    }

    [Fact]
    public void BuildFollowUpPrompt_NoQuestion_ReturnsNull()
    {
        // Arrange
        var toolCall = new ToolCallRequest
        {
            Tool = "ask_followup_question",
            Parameters = JsonDocument.Parse(/*lang=json,strict*/
                """{"options":["Option A"]}""").RootElement
        };

        // Act
        var result = _service.BuildFollowUpPrompt(toolCall);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void BuildFollowUpPrompt_EmptyQuestion_ReturnsNull()
    {
        // Arrange
        var toolCall = new ToolCallRequest
        {
            Tool = "ask_followup_question",
            Parameters = JsonDocument.Parse(/*lang=json,strict*/
                """{"question":"","options":[]}""").RootElement
        };

        // Act
        var result = _service.BuildFollowUpPrompt(toolCall);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void BuildTodoUpdate_FromParsedAiResponse_ReturnsItemsAndStatusText()
    {
        // Arrange
        var parsed = new ParsedAiResponse
        {
            ToolCall = MakeToolCall(/*lang=json,strict*/
                """{"tasks":[{"id":"10","description":"Deploy","status":"in_progress"}]}""")
        };

        // Act
        var result = _service.BuildTodoUpdate(parsed);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result.Items);
        Assert.Equal("10", result.Items[0].Id);
        Assert.Equal("Deploy", result.Items[0].Description);
        Assert.Equal("in_progress", result.Items[0].Status);
        Assert.Equal("Todo list updated: 1 task(s), 0 completed.", result.StatusText);
    }

    [Fact]
    public void BuildFollowUpPrompt_FromParsedAiResponse_ReturnsPrompt()
    {
        // Arrange
        var parsed = new ParsedAiResponse
        {
            ToolCall = new ToolCallRequest
            {
                Tool = "ask_followup_question",
                Parameters = JsonDocument.Parse(/*lang=json,strict*/
                    """{"question":"Which file?","options":["README.md","Program.cs"]}""").RootElement
            }
        };

        // Act
        var result = _service.BuildFollowUpPrompt(parsed);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Which file?", result.Question);
        Assert.Equal(2, result.Options.Count);
    }

    [Fact]
    public void BuildFollowUpPrompt_NoOptions_ReturnsQuestionWithEmptyOptions()
    {
        // Arrange
        var toolCall = new ToolCallRequest
        {
            Tool = "ask_followup_question",
            Parameters = JsonDocument.Parse(/*lang=json,strict*/
                """{"question":"Ready to continue?"}""").RootElement
        };

        // Act
        var result = _service.BuildFollowUpPrompt(toolCall);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Ready to continue?", result.Question);
        Assert.Empty(result.Options);
    }

    [Fact]
    public void BuildTodoUpdate_NoTasksProperty_ReturnsEmptyItems()
    {
        // Arrange
        var toolCall = MakeToolCall(/*lang=json,strict*/
            """{"other":"data"}""");

        // Act
        var result = _service.BuildTodoUpdate(toolCall);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result.Items);
        Assert.Equal("Todo list updated: 0 task(s), 0 completed.", result.StatusText);
    }
}
