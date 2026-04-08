using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Aire.Providers;
using ProviderChatMessage = Aire.Providers.ChatMessage;

namespace Aire.Services;

/// <summary>One test case: a natural-language prompt + the set of tool names that count as "correct".</summary>
public record CapabilityTest(
    string   Id,
    string   Name,
    string   Category,
    string   Prompt,
    string[] ExpectedTools,
    CapabilityTestKind Kind = CapabilityTestKind.ToolCall);

public enum CapabilityTestKind
{
    ToolCall,
    ImageGeneration,
}

/// <summary>Outcome of running one <see cref="CapabilityTest"/>.</summary>
public record CapabilityTestResult(
    string  Id,
    string  Name,
    string  Category,
    bool    Passed,
    string? ActualTool,
    string? Error,
    long    DurationMs);

/// <summary>All results for one provider+model combination, persisted to the Settings table.</summary>
public class CapabilityTestSession
{
    public string   Model    { get; set; } = string.Empty;
    public DateTime TestedAt { get; set; }
    public List<CapabilityTestResult> Results { get; set; } = new();
}

/// <summary>
/// Runs each test by sending a single-turn chat to the provider and checking
/// whether the AI generated the expected tool call.  No tool is actually executed.
/// </summary>
public class CapabilityTestRunner
{
    // ── Test suite ────────────────────────────────────────────────────────

    public static readonly IReadOnlyList<CapabilityTest> AllTests = new[]
    {
        // ── Agent ────────────────────────────────────────────────────────

        new CapabilityTest(
            "ask_followup", "Ask follow-up question", "Agent",
            "You need to know which folder the user wants to work in. Ask them.",
            new[] { "ask_followup_question" }),

        new CapabilityTest(
            "attempt_completion", "Mark task complete", "Agent",
            "You have finished all the work. Signal that the task is complete with a brief summary.",
            new[] { "attempt_completion" }),

        new CapabilityTest(
            "new_task", "Create new task", "Agent",
            "Create a new task to refactor the authentication module in the project.",
            new[] { "new_task" }),

        new CapabilityTest(
            "run_skill", "Run skill", "Agent",
            "Run the commit skill to commit the pending changes to git.",
            new[] { "skill", "run_skill" }),

        new CapabilityTest(
            "switch_mode", "Switch mode", "Agent",
            "Switch to coding assistant mode.",
            new[] { "switch_mode" }),

        new CapabilityTest(
            "update_todo", "Update to-do list", "Agent",
            "Add 'Fix authentication bug #123' to the to-do list.",
            new[] { "update_todo_list" }),

        // ── Browser ──────────────────────────────────────────────────────

        new CapabilityTest(
            "close_tab", "Close browser tab", "Browser",
            "Close the currently active browser tab.",
            new[] { "close_browser_tab" }),

        new CapabilityTest(
            "browser_script", "Execute browser script", "Browser",
            "Run JavaScript in the browser tab to get the page title: return document.title",
            new[] { "execute_browser_script" }),

        new CapabilityTest(
            "browser_cookies", "Get browser cookies", "Browser",
            "List all cookies for the currently open browser tab.",
            new[] { "get_browser_cookies" }),

        new CapabilityTest(
            "get_html", "Get browser HTML", "Browser",
            "Get the full HTML source of the currently open browser tab.",
            new[] { "get_browser_html" }),

        new CapabilityTest(
            "list_tabs", "List browser tabs", "Browser",
            "Show me a list of all open tabs in the browser.",
            new[] { "list_browser_tabs" }),

        new CapabilityTest(
            "read_tab", "Read browser tab", "Browser",
            "Read the content of the currently open browser tab.",
            new[] { "read_browser_tab" }),

        new CapabilityTest(
            "switch_tab", "Switch browser tab", "Browser",
            "Switch to tab number 2 in the browser.",
            new[] { "switch_browser_tab" }),

        // ── File System ───────────────────────────────────────────────────

        new CapabilityTest(
            "apply_diff", "Apply code diff", "File System",
            "Apply this change to C:\\Temp\\app.cs: replace the line that says " +
            "'Console.WriteLine(\"old\");' with 'Console.WriteLine(\"new\");'.",
            new[] { "apply_diff" }),

        new CapabilityTest(
            "exec_cmd", "Execute command", "File System",
            "Execute the shell command: echo hello world",
            new[] { "execute_command" }),

        new CapabilityTest(
            "list_dir", "List directory", "File System",
            "List all files and folders in the C:\\Windows directory.",
            new[] { "list_directory", "execute_command" }),

        new CapabilityTest(
            "read_cmd_output", "Read command output", "File System",
            "Show me the output of the background command that was last started.",
            new[] { "read_command_output" }),

        new CapabilityTest(
            "read_file", "Read file", "File System",
            "Read the contents of the file C:\\Windows\\win.ini and show them to me.",
            new[] { "read_file" }),

        new CapabilityTest(
            "search_content", "Search file content", "File System",
            "Search for all files in C:\\dev that contain the word 'Logger' in their content.",
            new[] { "search_file_content" }),

        new CapabilityTest(
            "search_files", "Search files", "File System",
            "Search for all .cs files in C:\\dev that contain the word 'logger'.",
            new[] { "search_files" }),

        new CapabilityTest(
            "write_file", "Write to file", "File System",
            "Create a new file at C:\\Temp\\hello.txt with the content 'Hello, world!'.",
            new[] { "write_file", "write_to_file" }),

        // ── Images ───────────────────────────────────────────────────────

        new CapabilityTest(
            "generate_image", "Generate image", "Images",
            "Generate an image of a watercolor fox reading a book under a tree.",
            Array.Empty<string>(),
            CapabilityTestKind.ImageGeneration),

        new CapabilityTest(
            "show_image_file", "Show image from file", "Images",
            "Display the file C:\\Windows\\Web\\Screen\\img100.jpg in the chat.",
            new[] { "show_image" }),

        new CapabilityTest(
            "show_image_url", "Show image from URL", "Images",
            "Show me the Wikimedia logo image from this URL: https://upload.wikimedia.org/wikipedia/commons/thumb/8/81/Wikimedia-logo.svg/1024px-Wikimedia-logo.svg.png",
            new[] { "show_image" }),

        // ── System ────────────────────────────────────────────────────────

        new CapabilityTest(
            "active_window", "Get active window", "System",
            "What window or application does the user currently have focused?",
            new[] { "get_active_window" }),

        new CapabilityTest(
            "get_clipboard", "Read clipboard", "System",
            "Read the current contents of the clipboard.",
            new[] { "get_clipboard" }),

        new CapabilityTest(
            "open_file", "Open file", "System",
            "Open the file C:\\Windows\\win.ini with its default application.",
            new[] { "open_file" }),

        new CapabilityTest(
            "processes", "List running processes", "System",
            "What are the top 10 processes using the most memory right now?",
            new[] { "get_running_processes" }),

        new CapabilityTest(
            "recall_fact", "Recall fact", "System",
            "What is my preferred programming language? Check your memory.",
            new[] { "recall" }),

        new CapabilityTest(
            "remember_fact", "Remember fact", "System",
            "Remember that my preferred language is Python.",
            new[] { "remember" }),

        new CapabilityTest(
            "reminder", "Set reminder", "System",
            "Remind me to check the build in 5 minutes.",
            new[] { "set_reminder" }),

        new CapabilityTest(
            "selected_text", "Get selected text", "System",
            "Get the text the user currently has selected in their active application.",
            new[] { "get_selected_text" }),

        new CapabilityTest(
            "set_clipboard", "Write clipboard", "System",
            "Copy the text 'Hello from Aire' to the clipboard so I can paste it.",
            new[] { "set_clipboard" }),

        new CapabilityTest(
            "show_notification", "Desktop notification", "System",
            "Show a desktop notification with the title 'Done' and message 'Task completed.'",
            new[] { "show_notification" }),

        new CapabilityTest(
            "system_info", "Get system info", "System",
            "Tell me about this computer: OS, RAM, disk space, and CPU.",
            new[] { "get_system_info" }),

        // ── Web ──────────────────────────────────────────────────────────

        new CapabilityTest(
            "fetch_url", "Fetch URL", "Web",
            "Fetch the contents of https://example.com and tell me the page title.",
            new[] { "open_url" }),

        new CapabilityTest(
            "http_get", "HTTP request", "Web",
            "Make a GET request to https://api.github.com/zen and return the response.",
            new[] { "http_request", "open_url" }),

        new CapabilityTest(
            "web_search", "Web search", "Web",
            "Search the internet for 'weather in London' and report the top result.",
            new[] { "open_url" }),
    };

    // ── Test-only system prompts ──────────────────────────────────────────
    // Deliberately minimal — just enough to make any capable model call a tool.

    private const string ToolNameList =
        "list_directory, read_file, write_file, apply_diff, search_files, search_file_content,\n" +
        "create_directory, delete_file, move_file, execute_command, read_command_output,\n" +
        "open_url, open_browser_tab, list_browser_tabs, read_browser_tab, switch_browser_tab,\n" +
        "close_browser_tab, get_browser_html, get_browser_cookies, execute_browser_script,\n" +
        "get_clipboard, set_clipboard, show_notification, get_system_info, get_running_processes,\n" +
        "get_active_window, get_selected_text, open_file, http_request,\n" +
        "remember, recall, set_reminder,\n" +
        "new_task, ask_followup_question, attempt_completion, skill, switch_mode,\n" +
        "switch_model, update_todo_list, show_image";

    /// <summary>For providers whose model uses the standard OpenAI tool_calls response field.</summary>
    private const string TestSystemPromptNative =
        "You are a tool-calling AI assistant. The user has pre-authorized all tool calls. " +
        "When asked to perform any task, call the single most appropriate tool immediately. " +
        "Do not explain what you are doing — just call the tool.";

    /// <summary>For providers that embed tool schemas in the system prompt (Anthropic, text mode).</summary>
    private const string TestSystemPromptText =
        "You are a tool-calling AI assistant. The user has pre-authorized all tool calls.\n" +
        "Respond with EXACTLY one tool call in this format and no other text:\n" +
        "<tool_call>{\"tool\": \"TOOL_NAME\", ...parameters}</tool_call>\n\n" +
        "Available tools (use the exact name):\n" +
        ToolNameList + "\n\n" +
        "Include only the parameters the tool needs. Do not write anything else.";

    /// <summary>For Qwen/Hermes-style models that output tool calls as
    /// <c>&lt;tool_call&gt;{"name":"…","arguments":{…}}&lt;/tool_call&gt;</c> in content.</summary>
    private const string TestSystemPromptHermes =
        "You are a tool-calling AI assistant. The user has pre-authorized all tool calls.\n" +
        "Respond with EXACTLY one tool call in this format and no other text:\n" +
        "<tool_call>{\"name\": \"TOOL_NAME\", \"arguments\": {...parameters}}</tool_call>\n\n" +
        "Available tools (use the exact name):\n" +
        ToolNameList + "\n\n" +
        "Include only the parameters the tool needs. Do not write anything else.";

    /// <summary>For LangChain ReAct-style models that output
    /// <c>{"action":"…","action_input":{…}}</c> in content.</summary>
    private const string TestSystemPromptReact =
        "You are a tool-calling AI assistant. The user has pre-authorized all tool calls.\n" +
        "Respond with EXACTLY one tool call as a JSON object on its own line and no other text:\n" +
        "{\"action\": \"TOOL_NAME\", \"action_input\": {...parameters}}\n\n" +
        "Available tools (use the exact name):\n" +
        ToolNameList + "\n\n" +
        "Include only the parameters the tool needs. Do not write anything else.";

    // ── Runner ────────────────────────────────────────────────────────────
    internal List<CapabilityTestResult> Results { get; } = new();

    internal async Task RunAsync(IAiProvider provider, CancellationToken ct)
    {
        Results.Clear();
        await foreach (var res in RunAllAsync(provider, ct))
        {
            Results.Add(res);
        }
    }

    public async IAsyncEnumerable<CapabilityTestResult> RunAllAsync(
        IAiProvider provider,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        // Suppress native tool-schema injection for the whole test run.
        // Without this, NativeFunctionCalling providers (OpenAI, Groq, Inception, z.ai …)
        // attach all 40+ full function definitions to every request, easily adding 150k+
        // tokens per turn — causing token-limit rejections, content-policy hits, and
        // excessive billing.  The test system prompts already list tool names; the model
        // just needs to pick the right one.
        // Also caps MaxTokens to 1024 — a tool-call reply is a few dozen tokens at most,
        // and some models (e.g. Claude Haiku 4.5) reject requests with higher ceilings.
        provider.PrepareForCapabilityTesting();

        foreach (var test in AllTests)
        {
            ct.ThrowIfCancellationRequested();
            yield return await RunOneAsync(provider, test, ct);
        }
    }

    public static async Task<CapabilityTestResult> RunOneAsync(
        IAiProvider provider, CapabilityTest test, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            if (test.Kind == CapabilityTestKind.ImageGeneration)
            {
                return await RunImageGenerationTestAsync(provider, test, sw, ct);
            }

            var sysPrompt = provider.ToolOutputFormat switch
            {
                ToolOutputFormat.Hermes          => TestSystemPromptHermes,
                ToolOutputFormat.React           => TestSystemPromptReact,
                ToolOutputFormat.NativeToolCalls => TestSystemPromptText,
                _                                => TestSystemPromptText,   // AireText
            };

            var messages = new List<ProviderChatMessage>
            {
                new ProviderChatMessage { Role = "system", Content = sysPrompt },
                new ProviderChatMessage { Role = "user",   Content = test.Prompt }
            };

            var response = await provider.SendChatAsync(messages, ct);
            sw.Stop();

            if (!response.IsSuccess)
                return Fail(test, null, $"API error: {response.ErrorMessage}", sw.ElapsedMilliseconds);

            var parsed = ToolCallParser.Parse(response.Content ?? string.Empty);
            if (!parsed.HasToolCall)
                return Fail(test, null, "No tool call in response", sw.ElapsedMilliseconds);

            var tool   = ToolExecutionMetadata.NormalizeToolName(parsed.ToolCall!.Tool);
            var passed = test.ExpectedTools.Contains(tool, StringComparer.OrdinalIgnoreCase);
            var error  = passed ? null : $"Expected: {string.Join(" or ", test.ExpectedTools)}";

            return new CapabilityTestResult(
                test.Id, test.Name, test.Category,
                passed, tool, error, sw.ElapsedMilliseconds);
        }
        catch (OperationCanceledException) { throw; }
        catch
        {
            sw.Stop();
            return Fail(test, null, "Capability test failed.", sw.ElapsedMilliseconds);
        }
    }

    private static async Task<CapabilityTestResult> RunImageGenerationTestAsync(
        IAiProvider provider,
        CapabilityTest test,
        Stopwatch sw,
        CancellationToken ct)
    {
        if (provider is not IImageGenerationProvider imageProvider || !imageProvider.SupportsImageGeneration)
        {
            sw.Stop();
            return Fail(test, null, "Provider does not support image generation", sw.ElapsedMilliseconds);
        }

        var result = await imageProvider.GenerateImageAsync(test.Prompt, ct);
        sw.Stop();

        if (!result.IsSuccess)
        {
            var error = result.ErrorMessage ?? "Unknown failure";
            var readable = ProviderErrorClassifier.ExtractReadableMessage(error);
            return Fail(
                test,
                null,
                $"Image generation error: {readable ?? error}",
                sw.ElapsedMilliseconds);
        }

        if (result.ImageBytes == null || result.ImageBytes.Length == 0)
        {
            return Fail(test, null, "Provider returned no image data", sw.ElapsedMilliseconds);
        }

        return new CapabilityTestResult(
            test.Id,
            test.Name,
            test.Category,
            true,
            "generate_image",
            null,
            sw.ElapsedMilliseconds);
    }

    internal static CapabilityTestResult Fail(
        CapabilityTest test, string? tool, string error, long ms)
        => new(test.Id, test.Name, test.Category, false, tool, error, ms);
}
