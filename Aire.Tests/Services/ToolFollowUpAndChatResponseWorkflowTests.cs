using System;
using System.Text.Json;
using Aire.Services;
using Aire.Services.Workflows;
using Xunit;

namespace Aire.Tests.Services;

public sealed class ToolFollowUpAndChatResponseWorkflowTests
{
    [Fact]
    public void ParseTodoTasks_HandlesJsonStringPlainTextAndInvalidShapes()
    {
        var service = new ToolFollowUpWorkflowService();

        using var jsonArrayStringDoc = JsonDocument.Parse("""
            {
              "tasks": "[{\"id\":\"1\",\"description\":\"Write tests\",\"status\":\"completed\"},{\"id\":\"2\",\"description\":\"Review code\",\"status\":\"pending\"}]"
            }
            """);
        using var plainTextDoc = JsonDocument.Parse("""
            {
              "tasks": "Just do the thing"
            }
            """);
        using var invalidShapeDoc = JsonDocument.Parse("""
            {
              "tasks": 123
            }
            """);

        var parsedJsonString = service.ParseTodoTasks(jsonArrayStringDoc.RootElement);
        var parsedPlainText = service.ParseTodoTasks(plainTextDoc.RootElement);
        var parsedInvalid = service.ParseTodoTasks(invalidShapeDoc.RootElement);

        Assert.Equal(2, parsedJsonString.Count);
        Assert.Equal("Write tests", parsedJsonString[0].Description);
        Assert.Equal("completed", parsedJsonString[0].Status);
        Assert.Single(parsedPlainText);
        Assert.Equal("Just do the thing", parsedPlainText[0].Description);
        Assert.Empty(parsedInvalid);
    }

    [Fact]
    public void ParseFollowUpQuestion_HandlesArrayJsonStringCsvAndMissingQuestion()
    {
        var service = new ToolFollowUpWorkflowService();

        using var jsonStringOptionsDoc = JsonDocument.Parse("""
            {
              "question": "Which one?",
              "options": "[\"One\",\"Two\"]"
            }
            """);
        using var csvOptionsDoc = JsonDocument.Parse("""
            {
              "question": "Pick one",
              "options": "Alpha, Beta\nGamma"
            }
            """);
        using var missingQuestionDoc = JsonDocument.Parse("""
            {
              "options": ["One"]
            }
            """);

        var jsonStringQuestion = service.ParseFollowUpQuestion(jsonStringOptionsDoc.RootElement);
        var csvQuestion = service.ParseFollowUpQuestion(csvOptionsDoc.RootElement);
        var missingQuestion = service.ParseFollowUpQuestion(missingQuestionDoc.RootElement);

        Assert.NotNull(jsonStringQuestion);
        Assert.Equal(["One", "Two"], jsonStringQuestion!.Options);
        Assert.NotNull(csvQuestion);
        Assert.Equal(["Alpha", "Beta", "Gamma"], csvQuestion!.Options);
        Assert.Null(missingQuestion);
    }

    [Fact]
    public void GetPathFromRequest_PrefersPathThenFromThenDirectory()
    {
        var service = new ToolFollowUpWorkflowService();

        Assert.Equal("C:\\repo\\a.txt", service.GetPathFromRequest(CreateRequest("{\"path\":\"C:\\\\repo\\\\a.txt\",\"from\":\"C:\\\\repo\\\\b.txt\"}")));
        Assert.Equal("C:\\repo\\b.txt", service.GetPathFromRequest(CreateRequest("{\"from\":\"C:\\\\repo\\\\b.txt\",\"directory\":\"C:\\\\repo\"}")));
        Assert.Equal("C:\\repo", service.GetPathFromRequest(CreateRequest("{\"directory\":\"C:\\\\repo\"}")));
        Assert.Equal(string.Empty, service.GetPathFromRequest(CreateRequest("{\"other\":\"value\"}")));
    }

    [Fact]
    public void BuildAssistantToolCallContent_HandlesEmptyAndPrefixedText()
    {
        var service = new ToolFollowUpWorkflowService();

        var withoutText = service.BuildAssistantToolCallContent(string.Empty, "{\"tool\":\"read_file\"}");
        var withText = service.BuildAssistantToolCallContent("Thinking", "{\"tool\":\"read_file\"}");

        Assert.Equal("<tool_call>{\"tool\":\"read_file\"}</tool_call>", withoutText);
        Assert.StartsWith("Thinking\n<tool_call>", withText, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildToolResultHistoryContent_TrimsVeryLargeResults()
    {
        var service = new ToolFollowUpWorkflowService();
        var largeResult = new string('x', 15_000);

        var history = service.BuildToolResultHistoryContent("get_browser_html", largeResult);

        Assert.Contains("[truncated", history, StringComparison.Ordinal);
        Assert.Contains("get_browser_html", history, StringComparison.Ordinal);
        Assert.True(history.Length < largeResult.Length);
    }

    [Fact]
    public void ChatResponseWorkflowService_HandlesWhitespacePreviewAndMissingCompletionResult()
    {
        var service = new ChatResponseWorkflowService();

        using var emptyResultDoc = JsonDocument.Parse("{}");
        using var nullResultDoc = JsonDocument.Parse("{\"result\":null}");

        Assert.Equal("(empty response)", service.NormalizeFinalText(string.Empty));
        Assert.Equal("   ", service.NormalizeFinalText("   "));
        Assert.Equal("short", service.BuildTrayPreview("short", 20));
        Assert.Equal("01234…", service.BuildTrayPreview("0123456789", 5));
        Assert.Equal(string.Empty, service.ExtractCompletionResult(new ToolCallRequest { Parameters = emptyResultDoc.RootElement.Clone() }));
        Assert.Equal(string.Empty, service.ExtractCompletionResult(new ToolCallRequest { Parameters = nullResultDoc.RootElement.Clone() }));
    }

    private static ToolCallRequest CreateRequest(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return new ToolCallRequest
        {
            Tool = "read_file",
            Parameters = doc.RootElement.Clone(),
            Description = "read_file",
            RawJson = json
        };
    }
}
