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
    public void GetToolDescription_CoversFilesystemBrowserAndTaskFlowBranches()
    {
        Assert.Equal("Fetch URL: https://example.com", _service.GetToolDescription(BuildRequest("open_url", "{\"url\":\"https://example.com\"}")));
        Assert.Equal("Open in browser: https://example.com", _service.GetToolDescription(BuildRequest("open_browser_tab", "{\"url\":\"https://example.com\"}")));
        Assert.Equal("List directory: C:/repo", _service.GetToolDescription(BuildRequest("list_directory", "{\"path\":\"C:/repo\"}")));
        Assert.Equal("Read file: C:/repo/app.cs", _service.GetToolDescription(BuildRequest("read_file", "{\"path\":\"C:/repo/app.cs\"}")));
        Assert.Equal("Create directory: C:/repo/new", _service.GetToolDescription(BuildRequest("create_directory", "{\"path\":\"C:/repo/new\"}")));
        Assert.Equal("Delete: C:/repo/old.txt", _service.GetToolDescription(BuildRequest("delete_file", "{\"path\":\"C:/repo/old.txt\"}")));
        Assert.Equal("Move: C:/from.txt → C:/to.txt", _service.GetToolDescription(BuildRequest("move_file", "{\"from\":\"C:/from.txt\",\"to\":\"C:/to.txt\"}")));
        Assert.Equal("Search ‘*.cs’ in: C:/repo", _service.GetToolDescription(BuildRequest("search_files", "{\"pattern\":\"*.cs\",\"directory\":\"C:/repo\"}")));
        Assert.Equal("New task: Fix bug", _service.GetToolDescription(BuildRequest("new_task", "{\"task\":\"Fix bug\"}")));
        Assert.Equal("Complete task: Done", _service.GetToolDescription(BuildRequest("attempt_completion", "{\"result\":\"Done\"}")));
        Assert.Equal("Ask: Which folder?", _service.GetToolDescription(BuildRequest("ask_followup_question", "{\"question\":\"Which folder?\"}")));
        Assert.Equal("Run skill: commit", _service.GetToolDescription(BuildRequest("skill", "{\"name\":\"commit\"}")));
        Assert.Equal("Switch mode: plan", _service.GetToolDescription(BuildRequest("switch_mode", "{\"mode\":\"plan\"}")));
        Assert.Equal("Update to-do list", _service.GetToolDescription(BuildRequest("update_todo_list", "{\"todos\":[]}")));
    }

    [Fact]
    public void GetToolDescription_CoversInputBrowserSystemAndEmailBranches()
    {
        Assert.Equal("Begin mouse session (5 min)", _service.GetToolDescription(BuildRequest("begin_mouse_session", "{\"duration_minutes\":5}")));
        Assert.Equal("End mouse session", _service.GetToolDescription(BuildRequest("end_mouse_session", "{}")));
        Assert.Equal("Take screenshot", _service.GetToolDescription(BuildRequest("take_screenshot", "{}")));
        Assert.Equal("Move mouse to (10, 20)", _service.GetToolDescription(BuildRequest("mouse_move", "{\"x\":10,\"y\":20}")));
        Assert.Equal("right click at (10, 20)", _service.GetToolDescription(BuildRequest("mouse_click", "{\"button\":\"right\",\"x\":10,\"y\":20}")));
        Assert.Equal("Double-click at (10, 20)", _service.GetToolDescription(BuildRequest("mouse_double_click", "{\"x\":10,\"y\":20}")));
        Assert.Equal("Drag (1,2) → (3,4)", _service.GetToolDescription(BuildRequest("mouse_drag", "{\"from_x\":1,\"from_y\":2,\"to_x\":3,\"to_y\":4}")));
        Assert.Equal("Type: hello", _service.GetToolDescription(BuildRequest("type_text", "{\"text\":\"hello\"}")));
        Assert.Equal("Key press: Enter", _service.GetToolDescription(BuildRequest("key_press", "{\"key\":\"Enter\"}")));
        Assert.Equal("Switch to browser tab 2", _service.GetToolDescription(BuildRequest("switch_browser_tab", "{\"index\":2}")));
        Assert.Equal("Close browser tab 1", _service.GetToolDescription(BuildRequest("close_browser_tab", "{\"index\":1}")));
        Assert.Equal("Get browser tab HTML", _service.GetToolDescription(BuildRequest("get_browser_html", "{}")));
        Assert.Equal("Run JS: alert(1)", _service.GetToolDescription(BuildRequest("execute_browser_script", "{\"script\":\"alert(1)\"}")));
        Assert.Equal("Get browser cookies", _service.GetToolDescription(BuildRequest("get_browser_cookies", "{}")));
        Assert.Equal("Read clipboard", _service.GetToolDescription(BuildRequest("get_clipboard", "{}")));
        Assert.Equal("Copy to clipboard: copied", _service.GetToolDescription(BuildRequest("set_clipboard", "{\"text\":\"copied\"}")));
        Assert.Equal("Get system info", _service.GetToolDescription(BuildRequest("get_system_info", "{}")));
        Assert.Equal("List running processes", _service.GetToolDescription(BuildRequest("get_running_processes", "{}")));
        Assert.Equal("Get active window", _service.GetToolDescription(BuildRequest("get_active_window", "{}")));
        Assert.Equal("Get selected text", _service.GetToolDescription(BuildRequest("get_selected_text", "{}")));
        Assert.Equal("Open file: C:/repo/app.cs", _service.GetToolDescription(BuildRequest("open_file", "{\"path\":\"C:/repo/app.cs\"}")));
        Assert.Equal("Remember: project", _service.GetToolDescription(BuildRequest("remember", "{\"key\":\"project\"}")));
        Assert.Equal("Recall: project", _service.GetToolDescription(BuildRequest("recall", "{\"key\":\"project\"}")));
        Assert.Equal("Remind in 15 min: Stand up", _service.GetToolDescription(BuildRequest("set_reminder", "{\"delay_minutes\":15,\"message\":\"Stand up\"}")));
        Assert.Equal("POST https://example.com", _service.GetToolDescription(BuildRequest("http_request", "{\"method\":\"post\",\"url\":\"https://example.com\"}")));
        Assert.Equal("Scroll at (5, 6)", _service.GetToolDescription(BuildRequest("mouse_scroll", "{\"x\":5,\"y\":6}")));
        Assert.Equal("Show image: C:/repo/image.png", _service.GetToolDescription(BuildRequest("show_image", "{\"path_or_url\":\"C:/repo/image.png\"}")));
        Assert.Equal("Reading emails", _service.GetToolDescription(BuildRequest("read_emails", "{}")));
        Assert.Equal("Sending email to a@example.com", _service.GetToolDescription(BuildRequest("send_email", "{\"to\":\"a@example.com\"}")));
        Assert.Equal("Searching emails for \"invoice\"", _service.GetToolDescription(BuildRequest("search_emails", "{\"query\":\"invoice\"}")));
        Assert.Equal("Replying to email", _service.GetToolDescription(BuildRequest("reply_to_email", "{}")));
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
    public void GetToolPath_CoversAdditionalBranches_AndFallbacks()
    {
        Assert.Equal("C:/repo", _service.GetToolPath(BuildRequest("execute_command", "{\"working_directory\":\"C:/repo\"}")));
        Assert.Equal(Environment.CurrentDirectory, _service.GetToolPath(BuildRequest("execute_command", "{}")));
        Assert.Equal("C:/repo", _service.GetToolPath(BuildRequest("list_directory", "{\"path\":\"C:/repo\"}")));
        Assert.Equal("C:/repo/file.txt", _service.GetToolPath(BuildRequest("write_file", "{\"path\":\"C:/repo/file.txt\"}")));
        Assert.Equal("C:/repo/new", _service.GetToolPath(BuildRequest("create_directory", "{\"path\":\"C:/repo/new\"}")));
        Assert.Equal("C:/repo/old.txt", _service.GetToolPath(BuildRequest("delete_file", "{\"path\":\"C:/repo/old.txt\"}")));
        Assert.Equal(string.Empty, _service.GetToolPath(BuildRequest("open_url", "{\"url\":\"https://example.com\"}")));
        Assert.Equal(string.Empty, _service.GetToolPath(new ToolCallRequest { Tool = "read_file" }));
        Assert.Equal(string.Empty, _service.GetToolPath(null));
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
