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
        Assert.Equal("Read example.txt?", parsedAiResponse.ToolCall.Description);
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
        Assert.Equal("Open Notepad?", parsedAiResponse.ToolCall.Description);
    }

    [Fact]
    public void Parse_BareJsonOnOwnLine_IsRecognized()
    {
        string response = "First I will do this.\r\n{\"tool\":\"search_files\",\"directory\":\"C:/repo\",\"pattern\":\"*.cs\"}";
        ParsedAiResponse parsedAiResponse = ToolCallParser.Parse(response);
        Assert.Equal("First I will do this.", parsedAiResponse.TextContent);
        Assert.NotNull(parsedAiResponse.ToolCall);
        Assert.Equal("search_files", parsedAiResponse.ToolCall.Tool);
        Assert.Contains("Search for", parsedAiResponse.ToolCall.Description);
        Assert.Contains("repo/", parsedAiResponse.ToolCall.Description);
    }

    [Fact]
    public void Parse_ArrayWrappedToolCall_UsesFirstEntry()
    {
        string response = "<tool_call>[\r\n  {\"tool\":\"open_url\",\"url\":\"https://example.com\"}\r\n]</tool_call>";
        ParsedAiResponse parsedAiResponse = ToolCallParser.Parse(response);
        Assert.NotNull(parsedAiResponse.ToolCall);
        Assert.Equal("open_url", parsedAiResponse.ToolCall.Tool);
        Assert.Equal("Fetch https://example.com?", parsedAiResponse.ToolCall.Description);
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
        Assert.Equal("Read demo.txt?", parsedAiResponse.ToolCall.Description);
    }

    [Fact]
    public void Parse_DetailsToolCall_WithIntegerAndUnderscoreKeys_IsRecognized()
    {
        string response = """
The browser tab is open. Now let me read the page to find the logo image URLs.
<details>
<summary>🎯 Tool call: show_image</summary>
Path_or_url: https://upload.wikimedia.org/wikipedia/commons/thumb/4/4d/OpenAI_Logo.svg/1200px-OpenAI_Logo.svg.png
Caption: OpenAI Logo
</details>
""";

        ParsedAiResponse parsedAiResponse = ToolCallParser.Parse(response);
        Assert.NotNull(parsedAiResponse.ToolCall);
        Assert.Equal("show_image", parsedAiResponse.ToolCall.Tool);
        Assert.Equal("https://upload.wikimedia.org/wikipedia/commons/thumb/4/4d/OpenAI_Logo.svg/1200px-OpenAI_Logo.svg.png", parsedAiResponse.ToolCall.Parameters.GetProperty("path_or_url").GetString());
        Assert.Equal("OpenAI Logo", parsedAiResponse.ToolCall.Parameters.GetProperty("caption").GetString());
    }

    [Fact]
    public void Parse_ArrayWrappedToolCalls_PreservesEveryRecognizedEntry()
    {
        string response = """
<tool_call>[
  {"tool":"open_browser_tab","url":"https://example.com"},
  {"tool":"read_browser_tab","index":-1},
  {"tool":"show_image","path_or_url":"https://example.com/logo.png","caption":"Logo"}
]</tool_call>
""";

        ParsedAiResponse parsedAiResponse = ToolCallParser.Parse(response);
        Assert.Equal(3, parsedAiResponse.ToolCalls.Count);
        Assert.Equal("open_browser_tab", parsedAiResponse.ToolCalls[0].Tool);
        Assert.Equal("read_browser_tab", parsedAiResponse.ToolCalls[1].Tool);
        Assert.Equal("show_image", parsedAiResponse.ToolCalls[2].Tool);
        Assert.Equal(-1, parsedAiResponse.ToolCalls[1].Parameters.GetProperty("index").GetInt32());
        Assert.Equal("Logo", parsedAiResponse.ToolCalls[2].Parameters.GetProperty("caption").GetString());
    }
}
