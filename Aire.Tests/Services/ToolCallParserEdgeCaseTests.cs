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
        Assert.True(parsedAiResponse.HasToolCall);
        Assert.Equal("test", parsedAiResponse.ToolCall.Tool);
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

    [Fact]
    public void Parse_XmlStyleToolCall_WithArgs_ExtractsToolAndParameters()
    {
        string response =
            "<tool_call name=\"list_files\">\n" +
            "  <arg name=\"path\" id=\"0\">C:\\dev\\mad</arg>\n" +
            "  <arg name=\"recursive\" id=\"1\">true</arg>\n" +
            "</tool_call>";

        ParsedAiResponse parsedAiResponse = ToolCallParser.Parse(response);

        Assert.True(parsedAiResponse.HasToolCall);
        Assert.Equal("list_files", parsedAiResponse.ToolCall?.Tool);
        Assert.Equal("C:\\dev\\mad", parsedAiResponse.ToolCall?.Parameters.GetProperty("path").GetString());
        Assert.True(parsedAiResponse.ToolCall?.Parameters.GetProperty("recursive").GetBoolean());
    }

    [Fact]
    public void Parse_PluralToolCallsTag_WithArgs_ExtractsToolAndParameters()
    {
        string response =
            "<tool_calls name=\"list_files\">\n" +
            "  <arg name=\"path\" id=\"0\">C:\\dev\\aire</arg>\n" +
            "  <arg name=\"recursive\" id=\"1\">false</arg>\n" +
            "</tool_calls>";

        ParsedAiResponse parsedAiResponse = ToolCallParser.Parse(response);

        Assert.True(parsedAiResponse.HasToolCall);
        Assert.Equal("list_files", parsedAiResponse.ToolCall?.Tool);
        Assert.Equal("C:\\dev\\aire", parsedAiResponse.ToolCall?.Parameters.GetProperty("path").GetString());
        Assert.False(parsedAiResponse.ToolCall?.Parameters.GetProperty("recursive").GetBoolean());
    }

    [Fact]
    public void Parse_PluralToolCallsJsonTag_ExtractsToolAndParameters()
    {
        string response =
            "<tool_calls>[{\"tool\":\"read_file\",\"path\":\"C:\\\\dev\\\\aire\\\\README.md\"}]</tool_calls>";

        ParsedAiResponse parsedAiResponse = ToolCallParser.Parse(response);

        Assert.True(parsedAiResponse.HasToolCall);
        Assert.Equal("read_file", parsedAiResponse.ToolCall?.Tool);
        Assert.Equal("C:\\dev\\aire\\README.md", parsedAiResponse.ToolCall?.Parameters.GetProperty("path").GetString());
    }

    [Fact]
    public void Parse_FolderStructureBlock_WithActionCreate_MapsToCreateDirectory()
    {
        string response =
            "<folder_structure>\n" +
            "  <action>create</action>\n" +
            "  <path>C:\\dev\\aire\\_deepseek_live2</path>\n" +
            "</folder_structure>";

        ParsedAiResponse parsedAiResponse = ToolCallParser.Parse(response);

        Assert.True(parsedAiResponse.HasToolCall);
        Assert.Equal("create_directory", parsedAiResponse.ToolCall?.Tool);
        Assert.Equal("C:\\dev\\aire\\_deepseek_live2", parsedAiResponse.ToolCall?.Parameters.GetProperty("path").GetString());
    }

    [Fact]
    public void Parse_TextPlusFolderStructureBlock_ExtractsToolAndKeepsVisibleText()
    {
        string response =
            "I'll start by creating the folder first.\n\n" +
            "<folder_structure>\n" +
            "  <action>create</action>\n" +
            "  <path>C:\\dev\\aire\\_deepseek_live3</path>\n" +
            "</folder_structure>";

        ParsedAiResponse parsedAiResponse = ToolCallParser.Parse(response);

        Assert.True(parsedAiResponse.HasToolCall);
        Assert.Equal("create_directory", parsedAiResponse.ToolCall?.Tool);
        Assert.Equal("C:\\dev\\aire\\_deepseek_live3", parsedAiResponse.ToolCall?.Parameters.GetProperty("path").GetString());
        Assert.Contains("I'll start by creating the folder first.", parsedAiResponse.TextContent);
    }

    [Fact]
    public void Parse_MixedStructuredAndXmlToolCalls_PreservesSourceOrder()
    {
        string response =
            "<tool_call>{\"tool\":\"read_file\",\"path\":\"C:\\\\dev\\\\aire\\\\README.md\"}</tool_call>\n" +
            "<file_action>\n" +
            "  <action>create</action>\n" +
            "  <path>C:\\dev\\aire\\notes.txt</path>\n" +
            "  <content>Hello</content>\n" +
            "</file_action>";

        ParsedAiResponse parsedAiResponse = ToolCallParser.Parse(response);

        Assert.True(parsedAiResponse.HasToolCall);
        Assert.Equal(2, parsedAiResponse.ToolCalls.Count);
        Assert.Equal("read_file", parsedAiResponse.ToolCalls[0].Tool);
        Assert.Equal("write_file", parsedAiResponse.ToolCalls[1].Tool);
        Assert.Equal("C:\\dev\\aire\\README.md", parsedAiResponse.ToolCalls[0].Parameters.GetProperty("path").GetString());
        Assert.Equal("C:\\dev\\aire\\notes.txt", parsedAiResponse.ToolCalls[1].Parameters.GetProperty("path").GetString());
    }

    [Fact]
    public void Parse_PluralToolCallsArray_PreservesArrayOrder()
    {
        string response =
            "<tool_calls>[{\"tool\":\"create_directory\",\"path\":\"C:\\\\dev\\\\aire\\\\one\"},{\"tool\":\"write_file\",\"path\":\"C:\\\\dev\\\\aire\\\\one\\\\note.txt\",\"content\":\"hello\"}]</tool_calls>";

        ParsedAiResponse parsedAiResponse = ToolCallParser.Parse(response);

        Assert.True(parsedAiResponse.HasToolCall);
        Assert.Equal(2, parsedAiResponse.ToolCalls.Count);
        Assert.Equal("create_directory", parsedAiResponse.ToolCalls[0].Tool);
        Assert.Equal("write_file", parsedAiResponse.ToolCalls[1].Tool);
        Assert.Equal("C:\\dev\\aire\\one", parsedAiResponse.ToolCalls[0].Parameters.GetProperty("path").GetString());
        Assert.Equal("C:\\dev\\aire\\one\\note.txt", parsedAiResponse.ToolCalls[1].Parameters.GetProperty("path").GetString());
    }

    [Fact]
    public void Parse_FileActionCreate_WithContent_MapsToWriteFile()
    {
        string response =
            "<file_action>\n" +
            "  <action>create</action>\n" +
            "  <path>C:\\dev\\aire\\notes.txt</path>\n" +
            "  <content>Hello from structured blocks</content>\n" +
            "</file_action>";

        ParsedAiResponse parsedAiResponse = ToolCallParser.Parse(response);

        Assert.True(parsedAiResponse.HasToolCall);
        Assert.Equal("write_file", parsedAiResponse.ToolCall?.Tool);
        Assert.Equal("C:\\dev\\aire\\notes.txt", parsedAiResponse.ToolCall?.Parameters.GetProperty("path").GetString());
        Assert.Equal("Hello from structured blocks", parsedAiResponse.ToolCall?.Parameters.GetProperty("content").GetString());
    }

    [Fact]
    public void Parse_FolderStructureDelete_ForDirectoryLikePath_DoesNotCreateFileDeleteTool()
    {
        string response =
            "<folder_structure>\n" +
            "  <action>delete</action>\n" +
            "  <path>C:\\dev\\aire\\notes</path>\n" +
            "</folder_structure>";

        ParsedAiResponse parsedAiResponse = ToolCallParser.Parse(response);

        Assert.False(parsedAiResponse.HasToolCall);
        Assert.DoesNotContain("delete_file", parsedAiResponse.TextContent, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parse_StandaloneToolTags_AreRemovedFromVisibleText()
    {
        string response =
            "Now I need to learn the real syntax by reading working examples.\n" +
            "<tool_call>\n" +
            "<tool_calls>\n" +
            "<tool_calls>\n";

        ParsedAiResponse parsedAiResponse = ToolCallParser.Parse(response);

        Assert.False(parsedAiResponse.HasToolCall);
        Assert.Contains("Now I need to learn the real syntax", parsedAiResponse.TextContent);
        Assert.DoesNotContain("<tool_call>", parsedAiResponse.TextContent, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<tool_calls>", parsedAiResponse.TextContent, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parse_FolderStructureList_WithoutPathStaysVisibleText()
    {
        string response =
            "I’m going to inspect the folder structure first.\n" +
            "<folder_structure>\n" +
            "  <action>list</action>\n" +
            "</folder_structure>";

        ParsedAiResponse parsedAiResponse = ToolCallParser.Parse(response);

        Assert.False(parsedAiResponse.HasToolCall);
        Assert.Contains("I’m going to inspect the folder structure first.", parsedAiResponse.TextContent);
        Assert.DoesNotContain("list_directory", parsedAiResponse.TextContent, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parse_TruncatedStructuredBlock_ReturnsCutOffWarning()
    {
        string response =
            "I’m going to inspect the folder structure first.\n" +
            "<folder_structure>\n" +
            "  <action>list</action>\n" +
            "  <path>C:\\dev\\aire</path>\n";

        ParsedAiResponse parsedAiResponse = ToolCallParser.Parse(response);

        Assert.False(parsedAiResponse.HasToolCall);
        Assert.Contains("cut off before the tool call could complete", parsedAiResponse.TextContent, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parse_RepeatedStructuredTags_DoesNotCrash()
    {
        string response =
            "<file_action>\n" +
            "  <action>create</action>\n" +
            "  <path>C:\\dev\\aire\\notes.txt</path>\n" +
            "  <path>C:\\dev\\aire\\notes-final.txt</path>\n" +
            "  <content>Hello</content>\n" +
            "</file_action>";

        Exception ex = Record.Exception(() => ToolCallParser.Parse(response));
        Assert.Null(ex);
    }

    [Fact]
    public void Parse_JsonToolCallContainingStructuredText_UsesJsonToolCall()
    {
        string response =
            "<tool_call>{\"tool\":\"write_file\",\"path\":\"C:\\\\dev\\\\aire\\\\notes.txt\",\"content\":\"<file_action><action>create</action><path>C:\\\\dev\\\\aire\\\\notes-final.txt</path></file_action>\"}</tool_call>";

        ParsedAiResponse parsedAiResponse = ToolCallParser.Parse(response);

        Assert.True(parsedAiResponse.HasToolCall);
        Assert.Single(parsedAiResponse.ToolCalls);
        Assert.Equal("write_file", parsedAiResponse.ToolCall?.Tool);
        Assert.Equal("C:\\dev\\aire\\notes.txt", parsedAiResponse.ToolCall?.Parameters.GetProperty("path").GetString());
    }

    [Fact]
    public void Parse_NestedStructuredBlockInsideToolCall_DoesNotDuplicate()
    {
        string response =
            "<tool_call>\n" +
            "  <file_action>\n" +
            "    <action>create</action>\n" +
            "    <path>C:\\dev\\aire\\notes.txt</path>\n" +
            "    <content>Hello</content>\n" +
            "  </file_action>\n" +
            "</tool_call>";

        ParsedAiResponse parsedAiResponse = ToolCallParser.Parse(response);

        Assert.True(parsedAiResponse.HasToolCall);
        Assert.Single(parsedAiResponse.ToolCalls);
        Assert.Equal("write_file", parsedAiResponse.ToolCall?.Tool);
        Assert.Equal("C:\\dev\\aire\\notes.txt", parsedAiResponse.ToolCall?.Parameters.GetProperty("path").GetString());
    }
}
