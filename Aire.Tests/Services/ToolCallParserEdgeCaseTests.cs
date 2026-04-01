using System;
using Aire.Services;
using Xunit;

namespace Aire.Tests.Services;

public class ToolCallParserEdgeCaseTests
{
    [Fact]
    public void Parse_EmptyString_ReturnsEmptyParsedResponse()
    {
        ParsedAiResponse parsedAiResponse = ToolCallParser.Parse("");
        Assert.False(parsedAiResponse.HasToolCall);
        Assert.Equal(string.Empty, parsedAiResponse.TextContent);
    }

    [Fact]
    public void Parse_UnclosedToolCallTag_ReturnsCutOffWarning()
    {
        string response = "Here is the start of a tool call: <tool_call>{\"tool\":\"test\"}";
        ParsedAiResponse parsedAiResponse = ToolCallParser.Parse(response);
        Assert.False(parsedAiResponse.HasToolCall);
        Assert.Contains("cut off", parsedAiResponse.TextContent);
    }

    [Fact]
    public void Parse_MalformedJsonInToolCall_DoesNotCrash()
    {
        string input = "<tool_call>{invalid_json_here}</tool_call>";
        Exception ex = Record.Exception(() => ToolCallParser.Parse(input));
        Assert.Null(ex);
    }

    [Fact]
    public void Parse_ValidJsonButMissingToolProperty_DoesNotCrash()
    {
        string input = "<tool_call>{\"not_tool\": \"value\"}</tool_call>";
        Exception ex = Record.Exception(() => ToolCallParser.Parse(input));
        Assert.Null(ex);
    }
}
