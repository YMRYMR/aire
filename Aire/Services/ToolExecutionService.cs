using System;
using System.Text.Json;
using System.Threading.Tasks;
using Aire.Services.Mcp;
using Aire.Services.Tools;
using static Aire.Services.Tools.ToolHelpers;

namespace Aire.Services
{
    /// <summary>
    /// Thin dispatcher that routes tool execution to the appropriate domain service.
    /// </summary>
    public class ToolExecutionService
    {
        private readonly FileSystemService      _fileSystemService;
        private readonly CommandExecutionService _commandService;
        internal readonly CommandToolService      _commandTool;
        private readonly WebToolService          _webTool;
        private readonly BrowserToolService      _browserTool;
        private readonly InputToolService        _inputTool;
        internal readonly SystemToolService       _systemTool;
        private readonly MemoryToolService       _memoryTool;
        private readonly AgentToolService        _agentTool;
        private readonly ContextInjectionToolService _contextTool;
        private readonly McpManager              _mcpManager;
        private readonly EmailToolService        _emailTool;

        /// <summary>
        /// Checks whether a tool name belongs to the keyboard-control family.
        /// </summary>
        public static bool IsKeyboardTool(string tool) => ToolExecutionMetadata.IsKeyboardTool(tool);

        /// <summary>
        /// Checks whether a tool name belongs to the mouse-control family.
        /// </summary>
        public static bool IsMouseTool(string tool) => ToolExecutionMetadata.IsMouseTool(tool);

        /// <summary>
        /// Checks whether a tool requires an input-control session.
        /// </summary>
        public static bool IsSessionTool(string tool) => ToolExecutionMetadata.IsSessionTool(tool);

        /// <summary>
        /// Creates the tool dispatcher and wires each domain-specific execution service.
        /// </summary>
        /// <param name="fileSystemService">File system tool implementation.</param>
        /// <param name="commandService">Command execution backend used by command tools.</param>
        /// <param name="hideWindowAsync">Optional callback used by input tools before interacting with the desktop.</param>
        /// <param name="showWindowAsync">Optional callback used by input tools after desktop interaction finishes.</param>
        /// <param name="mcpManager">Optional MCP manager override.</param>
        /// <param name="emailTool">Optional email tool override.</param>
        public ToolExecutionService(
            FileSystemService fileSystemService,
            CommandExecutionService commandService,
            Func<Task>? hideWindowAsync = null,
            Func<Task>? showWindowAsync = null,
            McpManager? mcpManager = null,
            EmailToolService? emailTool = null)
        {
            _fileSystemService = fileSystemService ?? throw new ArgumentNullException(nameof(fileSystemService));
            _commandService    = commandService    ?? throw new ArgumentNullException(nameof(commandService));

            var webFetch    = new WebFetchService();

            _commandTool = new CommandToolService(_commandService);
            _webTool     = new WebToolService(webFetch);
            _browserTool = new BrowserToolService();
            _inputTool   = new InputToolService(hideWindowAsync, showWindowAsync);
            _systemTool  = new SystemToolService();
            _memoryTool  = new MemoryToolService();
            _agentTool   = new AgentToolService();
            _contextTool = new ContextInjectionToolService();
            _mcpManager  = mcpManager ?? McpManager.Instance;
            _emailTool   = emailTool  ?? new EmailToolService(new Aire.Data.DatabaseService());
        }

        /// <summary>
        /// Executes a tool exposed by an MCP server and converts the result into the app's tool result shape.
        /// </summary>
        /// <param name="request">Normalized tool call request targeting an MCP-backed tool.</param>
        /// <returns>The MCP result wrapped as a standard tool execution result.</returns>
        private async Task<ToolExecutionResult> ExecuteMcpToolAsync(ToolCallRequest request)
        {
            var result = await _mcpManager.ExecuteToolAsync(request.Tool, request.Parameters);
            return new ToolExecutionResult
            {
                TextResult = result.IsError ? $"ERROR: {result.Text}" : result.Text
            };
        }

        /// <summary>
        /// Normalizes aliases and synonyms to the canonical tool name used internally.
        /// </summary>
        /// <param name="tool">Tool name supplied by the model or API caller.</param>
        /// <returns>The canonical tool name used by the dispatcher.</returns>
        internal static string NormalizeToolName(string tool) => ToolExecutionMetadata.NormalizeToolName(tool);

        /// <summary>
        /// Executes a tool request by routing it to the appropriate domain service.
        /// </summary>
        /// <param name="request">Normalized tool request to execute.</param>
        /// <returns>The execution result, including either tool output or a normalized error string.</returns>
        public async Task<ToolExecutionResult> ExecuteAsync(ToolCallRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            if (string.IsNullOrEmpty(request.Tool))
                return new ToolExecutionResult { TextResult = "Error: No tool specified" };

            request.Tool = NormalizeToolName(request.Tool);

            try
            {
                return request.Tool switch
                {
                    "execute_command"        => await _commandTool.ExecuteAsync(request),
                    "open_url"               => await _webTool.ExecuteOpenUrlAsync(request),
                    "http_request"           => await _webTool.ExecuteHttpRequestAsync(request),
                    "open_browser_tab"       => _browserTool.ExecuteOpenBrowserTab(request),
                    "list_browser_tabs"      => _browserTool.ExecuteListBrowserTabs(),
                    "read_browser_tab"       => await _browserTool.ExecuteReadBrowserTabAsync(request),
                    "switch_browser_tab"     => _browserTool.ExecuteSwitchBrowserTab(request),
                    "close_browser_tab"      => _browserTool.ExecuteCloseBrowserTab(request),
                    "get_browser_html"       => await _browserTool.ExecuteGetBrowserHtmlAsync(request),
                    "execute_browser_script" => await _browserTool.ExecuteBrowserScriptAsync(request),
                    "get_browser_cookies"    => await _browserTool.ExecuteGetBrowserCookiesAsync(request),
                    "get_clipboard"          => SystemToolService.ExecuteGetClipboard(),
                    "set_clipboard"          => SystemToolService.ExecuteSetClipboard(request),
                    "show_notification"      => SystemToolService.ExecuteNotify(request),
                    "get_system_info"        => SystemToolService.ExecuteGetSystemInfo(),
                    "get_running_processes"  => SystemToolService.ExecuteGetRunningProcesses(request),
                    "get_active_window"      => SystemToolService.ExecuteGetActiveWindow(),
                    "get_selected_text"      => await SystemToolService.ExecuteGetSelectedTextAsync(),
                    "open_file"              => SystemToolService.ExecuteOpenFile(request),
                    "remember"               => _memoryTool.ExecuteRemember(request),
                    "recall"                 => _memoryTool.ExecuteRecall(request),
                    "set_reminder"           => _memoryTool.ExecuteSetReminder(request),
                    "show_image"             => await _agentTool.ExecuteShowImageAsync(request),
                    "request_context"        => await _contextTool.ExecuteAsync(request),
                    "skill"                  => BuiltinToolSkillService.Execute(request),
                    "read_emails" or "send_email" or "search_emails" or "reply_to_email"
                                             => await _emailTool.ExecuteAsync(request),
                    var t when IsSessionTool(t) => await _inputTool.ExecuteAsync(request),
                    var t when _mcpManager.IsToolMcp(t) => await ExecuteMcpToolAsync(request),
                    _                        => await _fileSystemService.ExecuteAsync(request)
                };
            }
            catch
            {
                AppLogger.Warn(nameof(ToolExecutionService), $"Unhandled error executing tool '{request.Tool}'.");
                return new ToolExecutionResult { TextResult = "ERROR: Tool execution failed." };
            }
        }

        /// <summary>
        /// Builds a human-readable description of a tool request for chat approval UI and trace output.
        /// </summary>
        /// <param name="request">Tool request to describe.</param>
        /// <returns>A concise description of the requested action.</returns>
        public string GetToolDescription(ToolCallRequest request)
            => ToolExecutionDescriptions.Describe(request, _commandTool, _commandService, _mcpManager);

        /// <summary>
        /// Gets the primary filesystem path affected by a tool request for audit logging.
        /// </summary>
        /// <param name="request">Tool request whose primary path should be extracted.</param>
        /// <returns>The main path touched by the request, or an empty string when none applies.</returns>
        public string GetToolPath(ToolCallRequest request)
            => ToolExecutionDescriptions.GetPath(request);

        /// <summary>
        /// Forwards a tray notification request to the system tool implementation.
        /// </summary>
        /// <param name="title">Short notification title.</param>
        /// <param name="message">Notification body text.</param>
        public static void ShowSystemNotification(string title, string message)
            => SystemToolService.ShowSystemNotification(title, message);
    }
}
