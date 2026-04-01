using Aire.Services;
using Xunit;

namespace Aire.Tests.Core;

public class ToolCallParserCoverageTests
{
    [Fact]
    public void Parse_ValidWrappedToolCall_ReturnsTextAndTool()
    {
        string response = "I will read the file.\r\n<tool_call>{\"tool\":\"read_file\",\"path\":\"C:/temp/example.txt\"}</tool_call>";
        ParsedAiResponse parsedAiResponse = ToolCallParser.Parse(response);
        Assert.Equal("I will read the file.", parsedAiResponse.TextContent);
        Assert.NotNull(parsedAiResponse.ToolCall);
        Assert.Equal("read_file", parsedAiResponse.ToolCall.Tool);
        Assert.Equal("Reading example.txt", parsedAiResponse.ToolCall.Description);
    }

    [Fact]
    public void Parse_StripsThinkBlockBeforeValidToolCall()
    {
        string response = "<think>\r\nExample only:\r\n<tool_call>{\"tool\":</tool_call>\r\n</think>\r\nReal response\r\n<tool_call>{\"tool\":\"read_file\",\"path\":\"C:/temp/real.txt\"}</tool_call>";
        ParsedAiResponse parsedAiResponse = ToolCallParser.Parse(response);
        Assert.Contains("Real response", parsedAiResponse.TextContent);
        Assert.NotNull(parsedAiResponse.ToolCall);
        Assert.Equal("read_file", parsedAiResponse.ToolCall.Tool);
    }

    [Fact]
    public void Parse_CodeFencedBareJson_IsRecognized()
    {
        string response = "```json\r\n{\"tool\":\"execute_command\",\"command\":\"notepad.exe\"}\r\n```";
        ParsedAiResponse parsedAiResponse = ToolCallParser.Parse(response);
        Assert.NotNull(parsedAiResponse.ToolCall);
        Assert.Equal("execute_command", parsedAiResponse.ToolCall.Tool);
        Assert.Equal("Opening Notepad", parsedAiResponse.ToolCall.Description);
    }

    [Fact]
    public void Parse_BareJsonOnOwnLine_IsRecognized()
    {
        string response = "First I will do this.\r\n{\"tool\":\"search_files\",\"directory\":\"C:/repo\",\"pattern\":\"*.cs\"}";
        ParsedAiResponse parsedAiResponse = ToolCallParser.Parse(response);
        Assert.Equal("First I will do this.", parsedAiResponse.TextContent);
        Assert.NotNull(parsedAiResponse.ToolCall);
        Assert.Equal("search_files", parsedAiResponse.ToolCall.Tool);
        Assert.Contains("Searching for", parsedAiResponse.ToolCall.Description);
        Assert.Contains("repo/", parsedAiResponse.ToolCall.Description);
    }

    [Fact]
    public void Parse_ArrayWrappedToolCall_UsesFirstEntry()
    {
        string response = "<tool_call>[\r\n  {\"tool\":\"open_url\",\"url\":\"https://example.com\"}\r\n]</tool_call>";
        ParsedAiResponse parsedAiResponse = ToolCallParser.Parse(response);
        Assert.NotNull(parsedAiResponse.ToolCall);
        Assert.Equal("open_url", parsedAiResponse.ToolCall.Tool);
        Assert.Equal("Fetching https://example.com", parsedAiResponse.ToolCall.Description);
    }

    [Fact]
    public void Parse_TrailingGarbageAfterJson_StillParses()
    {
        string response = "<tool_call>{\"tool\":\"open_url\",\"url\":\"https://example.com\"}]</tool_call>";
        ParsedAiResponse parsedAiResponse = ToolCallParser.Parse(response);
        Assert.NotNull(parsedAiResponse.ToolCall);
        Assert.Equal("open_url", parsedAiResponse.ToolCall.Tool);
    }

    [Fact]
    public void Parse_TruncatedToolCall_ReturnsWarningText()
    {
        string response = "Starting now\r\n<tool_call>{\"tool\":\"read_file\",\"path\":\"C:/temp/file.txt\"";
        ParsedAiResponse parsedAiResponse = ToolCallParser.Parse(response);
        Assert.Contains("cut off", parsedAiResponse.TextContent);
        Assert.Null(parsedAiResponse.ToolCall);
    }

    [Fact]
    public void Parse_InvalidToolJson_StripsToolBlockFromText()
    {
        string response = "Before\r\n<tool_call>{\"tool\":</tool_call>\r\nAfter";
        ParsedAiResponse parsedAiResponse = ToolCallParser.Parse(response);
        Assert.Equal("Before\n\nAfter", parsedAiResponse.TextContent.Replace("\r", ""));
        Assert.Null(parsedAiResponse.ToolCall);
    }

    [Fact]
    public void Parse_FullwidthQuotesAndPunctuation_AreNormalized()
    {
        string response = "<tool_call>{\uFF02tool\uFF02\uFF1A\uFF02read_file\uFF02\uFF0C\uFF02path\uFF02\uFF1A\uFF02C:/temp/demo.txt\uFF02}</tool_call>";
        ParsedAiResponse parsedAiResponse = ToolCallParser.Parse(response);
        Assert.NotNull(parsedAiResponse.ToolCall);
        Assert.Equal("read_file", parsedAiResponse.ToolCall.Tool);
        Assert.Equal("Reading demo.txt", parsedAiResponse.ToolCall.Description);
    }
}
