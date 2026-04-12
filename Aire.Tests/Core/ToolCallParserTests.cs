using System;
using Aire.Services;
using Xunit;

namespace Aire.Tests.Core;

[Collection("NonParallelCoreUtilities")]
public class ToolCallParserTests
{
    [Fact]
    public void Parse_PlainText_ReturnsTextWithoutToolCall()
    {
        ParsedAiResponse parsedAiResponse = ToolCallParser.Parse("Just answer normally.");
        Assert.False(parsedAiResponse.HasToolCall);
        Assert.Equal("Just answer normally.", parsedAiResponse.TextContent);
    }

    [Fact]
    public void Parse_TaggedToolCall_ReturnsToolAndDescription()
    {
        ParsedAiResponse parsedAiResponse = ToolCallParser.Parse("I will do that.\r\n<tool_call>{\"tool\":\"read_file\",\"path\":\"C:/tmp/example.txt\"}</tool_call>");
        Assert.True(parsedAiResponse.HasToolCall);
        Assert.Equal("I will do that.", parsedAiResponse.TextContent);
        Assert.Equal("read_file", parsedAiResponse.ToolCall.Tool);
        Assert.Equal("Read example.txt?", parsedAiResponse.ToolCall.Description);
    }

    [Fact]
    public void Parse_StripsThinkBlocks_BeforeReturningText()
    {
        ParsedAiResponse parsedAiResponse = ToolCallParser.Parse("<think>I should use a tool</think>\r\nVisible text\r\n<tool_call>{\"tool\":\"open_url\",\"url\":\"https://example.com\"}</tool_call>");
        Assert.True(parsedAiResponse.HasToolCall);
        Assert.Equal("Visible text", parsedAiResponse.TextContent);
        Assert.Equal("Fetch https://example.com?", parsedAiResponse.ToolCall.Description);
    }

    [Fact]
    public void Parse_BareJsonInCodeFence_NormalizesIntoToolCall()
    {
        ParsedAiResponse parsedAiResponse = ToolCallParser.Parse("```json\r\n{\"tool\":\"execute_command\",\"command\":\"notepad\"}\r\n```");
        Assert.True(parsedAiResponse.HasToolCall);
        Assert.Equal("execute_command", parsedAiResponse.ToolCall.Tool);
        Assert.Equal("Open Notepad?", parsedAiResponse.ToolCall.Description);
    }

    [Fact]
    public void Parse_BareJsonOnOwnLine_NormalizesIntoToolCall()
    {
        ParsedAiResponse parsedAiResponse = ToolCallParser.Parse("First line\r\n{\"tool\":\"search_files\",\"directory\":\"C:/repo\",\"pattern\":\"*.cs\"}");
        Assert.True(parsedAiResponse.HasToolCall);
        Assert.Equal("First line", parsedAiResponse.TextContent);
        Assert.True(parsedAiResponse.ToolCall.Description.Contains("Search for") && parsedAiResponse.ToolCall.Description.Contains("*.cs"), "Expected tool description for search");
    }

    [Fact]
    public void Parse_ArrayWrappedToolCall_UsesFirstElement()
    {
        ParsedAiResponse parsedAiResponse = ToolCallParser.Parse("<tool_call>[{\"tool\":\"switch_mode\",\"mode\":\"code\"}]</tool_call>");
        Assert.True(parsedAiResponse.HasToolCall);
        Assert.Equal("Switch to code mode?", parsedAiResponse.ToolCall.Description);
    }

    [Fact]
    public void Parse_TypographicQuotes_AreNormalized()
    {
        ParsedAiResponse parsedAiResponse = ToolCallParser.Parse("<tool_call>{\"tool\":\"open_url\",\"url\":\"https://example.com/docs\"}</tool_call>".Replace("\"", "“"));
        Assert.True(parsedAiResponse.HasToolCall);
        Assert.Equal("open_url", parsedAiResponse.ToolCall.Tool);
        Assert.Equal("Fetch https://example.com/docs?", parsedAiResponse.ToolCall.Description);
    }

    [Fact]
    public void Parse_TruncatedToolCall_ReturnsCutoffWarning()
    {
        ParsedAiResponse parsedAiResponse = ToolCallParser.Parse("Starting...\r\n<tool_call>{\"tool\":\"read_file\",\"path\":\"C:/tmp/test.txt\"}");
        Assert.True(parsedAiResponse.HasToolCall);
        Assert.Equal("read_file", parsedAiResponse.ToolCall.Tool);
    }

    [Fact]
    public void Parse_InvalidToolCallBlock_FallsBackToCleanedText()
    {
        ParsedAiResponse parsedAiResponse = ToolCallParser.Parse("Prefix\r\n<tool_call>{not valid json</tool_call>\r\nSuffix");
        Assert.False(parsedAiResponse.HasToolCall);
        Assert.Equal("Prefix\r\n\r\nSuffix", parsedAiResponse.TextContent);
    }

    [Fact]
    public void Parse_MoveFile_ProducesFriendlyDescription()
    {
        ParsedAiResponse parsedAiResponse = ToolCallParser.Parse("<tool_call>{\"tool\":\"move_file\",\"from\":\"C:/from/a.txt\",\"to\":\"C:/to/b.txt\"}</tool_call>");
        Assert.True(parsedAiResponse.HasToolCall);
        Assert.Equal("Move a.txt → b.txt?", parsedAiResponse.ToolCall.Description);
    }

    [Fact]
    public void Parse_MarkdownDetailsToolCall_NormalizesIntoToolCall()
    {
        string response = """
Let me find the OpenAI Codex logo online and show it to you.
<details>
<summary>🎯 Tool call: open_browser_tab</summary>
Url: https://www.google.com/search?q=OpenAI+Codex+logo&tbm=isch
</details>
""";

        ParsedAiResponse parsedAiResponse = ToolCallParser.Parse(response);
        Assert.True(parsedAiResponse.HasToolCall);
        Assert.Equal("Let me find the OpenAI Codex logo online and show it to you.", parsedAiResponse.TextContent);
        Assert.Equal("open_browser_tab", parsedAiResponse.ToolCall.Tool);
        Assert.Equal("https://www.google.com/search?q=OpenAI+Codex+logo&tbm=isch", parsedAiResponse.ToolCall.Parameters.GetProperty("url").GetString());
    }

    [Fact]
    public void Parse_MultipleMarkdownDetailsToolCalls_PreservesAllCallsInOrder()
    {
        string response = """
Let me find the OpenAI Codex logo online and show it to you.
<details>
<summary>🎯 Tool call: open_browser_tab</summary>
Url: https://www.google.com/search?q=OpenAI+Codex+logo&tbm=isch
</details>
The browser tab is open. Now let me read the page to find the logo image URLs.
<details>
<summary>🎯 Tool call: read_browser_tab</summary>
Index: -1
</details>
<details>
<summary>🎯 Tool call: execute_browser_script</summary>
Script: JSON.stringify(Array.from(document.querySelectorAll('img')).slice(0,10).map(i=>({src:i.src,alt:i.alt})))
</details>
""";

        ParsedAiResponse parsedAiResponse = ToolCallParser.Parse(response);
        Assert.True(parsedAiResponse.HasToolCall);
        Assert.Equal(3, parsedAiResponse.ToolCalls.Count);
        Assert.Equal("open_browser_tab", parsedAiResponse.ToolCalls[0].Tool);
        Assert.Equal("read_browser_tab", parsedAiResponse.ToolCalls[1].Tool);
        Assert.Equal("execute_browser_script", parsedAiResponse.ToolCalls[2].Tool);
        Assert.Contains("The browser tab is open. Now let me read the page to find the logo image URLs.", parsedAiResponse.TextContent);
        Assert.Equal(-1, parsedAiResponse.ToolCalls[1].Parameters.GetProperty("index").GetInt32());
    }
}
