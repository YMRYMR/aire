using System;
using System.Text.Json;
using System.Threading.Tasks;
using Aire.Services;
using Xunit;

namespace Aire.Tests.Services;

public class ToolExecutionServiceDescriptionTests
{
    private readonly ToolExecutionService _service = new ToolExecutionService(new FileSystemService(), new CommandExecutionService());

    [Fact]
    public void NormalizeToolName_MapsKnownAliases()
    {
        Assert.Equal("read_browser_tab", ToolExecutionMetadata.NormalizeToolName("read_tab"));
        Assert.Equal("set_clipboard", ToolExecutionMetadata.NormalizeToolName("copy_to_clipboard"));
        Assert.Equal("search_file_content", ToolExecutionMetadata.NormalizeToolName("grep"));
    }

    [Fact]
    public void GetToolDescription_ReturnsFriendlyDescriptions()
    {
        ToolCallRequest request = BuildRequest("execute_command", "{\"command\":\"notepad.exe\"}");
        ToolCallRequest request2 = BuildRequest("search_file_content", "{\"pattern\":\"TODO\",\"directory\":\"C:/repo\"}");
        ToolCallRequest request3 = BuildRequest("show_notification", "{\"title\":\"Heads up\"}");
        Assert.Contains("Execute", _service.GetToolDescription(request), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("TODO", _service.GetToolDescription(request2));
        Assert.Contains("Heads up", _service.GetToolDescription(request3));
    }

    [Fact]
    public void GetToolPath_ReturnsRelevantPaths()
    {
        ToolCallRequest request = BuildRequest("move_file", "{\"from\":\"C:/from.txt\",\"to\":\"C:/to.txt\"}");
        ToolCallRequest request2 = BuildRequest("search_files", "{\"directory\":\"C:/repo\"}");
        ToolCallRequest request3 = BuildRequest("execute_command", "{\"working_directory\":\"C:/work\"}");
        Assert.Equal("C:/from.txt", _service.GetToolPath(request));
        Assert.Equal("C:/repo", _service.GetToolPath(request2));
        Assert.Equal("C:/work", _service.GetToolPath(request3));
    }

    [Fact]
    public void KeyboardMouseAndSessionPredicates_MatchMetadata()
    {
        Assert.True(ToolExecutionService.IsKeyboardTool("key_press"));
        Assert.True(ToolExecutionService.IsMouseTool("mouse_click"));
        Assert.True(ToolExecutionService.IsSessionTool("take_screenshot"));
        Assert.False(ToolExecutionService.IsSessionTool("read_file"));
    }

    [Fact]
    public async Task ExecuteAsync_RejectsNullAndEmptyTool()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => _service.ExecuteAsync(null));
        Assert.Contains("No tool specified", (await _service.ExecuteAsync(new ToolCallRequest())).TextResult);
    }

    [Fact]
    public async Task ExecuteAsync_CoversSafeSystemAndEmailBranches()
    {
        ToolExecutionResult systemInfo = await _service.ExecuteAsync(new ToolCallRequest
        {
            Tool = "get_system_info"
        });
        ToolExecutionResult processes = await _service.ExecuteAsync(new ToolCallRequest
        {
            Tool = "get_running_processes",
            Parameters = JsonDocument.Parse("{\"top_n\":1,\"filter\":\"unlikely-process-name-xyz\"}").RootElement.Clone()
        });
        ToolExecutionResult openFile = await _service.ExecuteAsync(new ToolCallRequest
        {
            Tool = "open_file",
            Parameters = JsonDocument.Parse("{}").RootElement.Clone()
        });
        ToolExecutionResult readEmails = await _service.ExecuteAsync(new ToolCallRequest
        {
            Tool = "read_emails",
            Parameters = JsonDocument.Parse("{}").RootElement.Clone()
        });
        Assert.Contains("OS:", systemInfo.TextResult);
        Assert.Contains("Process", processes.TextResult);
        Assert.Contains("path parameter is required", openFile.TextResult);
        Assert.True(readEmails.TextResult.Contains("No email account configured", StringComparison.OrdinalIgnoreCase) || readEmails.TextResult.Contains("Email error", StringComparison.OrdinalIgnoreCase));
    }

    private static ToolCallRequest BuildRequest(string tool, string json)
    {
        using JsonDocument jsonDocument = JsonDocument.Parse(json);
        return new ToolCallRequest
        {
            Tool = tool,
            Parameters = jsonDocument.RootElement.Clone(),
            RawJson = json
        };
    }
}
