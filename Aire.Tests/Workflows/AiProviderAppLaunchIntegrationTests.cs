using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Aire.Data;
using Aire.Providers;
using Xunit;

namespace Aire.Tests.Workflows;

public class AiProviderAppLaunchIntegrationTests : IAsyncLifetime, IDisposable
{
    private static readonly string ProductionDbPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Aire", "aire.db");

    private DatabaseService? _db;

    private List<Provider> _providers = new List<Provider>();

    private static bool LiveProvidersEnabled => string.Equals(Environment.GetEnvironmentVariable("AIRE_RUN_LIVE_PROVIDER_TESTS"), "1", StringComparison.Ordinal);

    public async Task InitializeAsync()
    {
        if (LiveProvidersEnabled && File.Exists(ProductionDbPath))
        {
            _db = new DatabaseService(ProductionDbPath);
            await _db.InitializeAsync();
            _providers = await _db.GetProvidersAsync();
        }
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _db?.Dispose();
    }

    private IAiProvider? GetProvider(string nameContains)
    {
        if (_db == null)
        {
            return null;
        }
        Provider provider = _providers.FirstOrDefault((Provider p) => p.Name.Contains(nameContains, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(p.ApiKey) && p.ApiKey != "claude.ai-session" && p.IsEnabled);
        if (provider == null)
        {
            return null;
        }
        ProviderFactory providerFactory = new ProviderFactory(_db);
        return providerFactory.CreateProvider(provider);
    }

    private static IReadOnlyList<ChatMessage> BuildMessages(string userMessage)
    {
        return new ChatMessage[2]
        {
            new ChatMessage
            {
                Role = "system",
                Content = "You are an AI assistant running inside the Aire Tray Chat application. The application provides you with tools to interact with the user's system.\n\nCRITICAL RULES — you MUST follow these without exception:\n1. NEVER say 'I cannot', 'I am unable to', or 'I'm just an AI' when asked to perform system tasks. You HAVE tools. Use them.\n1a. When summarising news or articles, ALWAYS include each article's full URL from the feed's Link: field verbatim in your reply — the user can click links in the chat.\n2. NEVER say you cannot launch, open, or start applications. You CAN launch any installed application — including GUI apps like GIMP, Notepad, Chrome, VLC — by using execute_command with just the app name (e.g. command=\"gimp\").\n3. When the user asks you to list, read, find, create, edit, move, or delete anything on the file system, you MUST emit a tool call immediately.\n4. When the user asks you to run commands, open applications, or perform any system operation, you MUST use the execute_command tool.\n5. After receiving a tool result, you MUST respond with a summary AND then IMMEDIATELY call the next tool if the user's task is not yet fully complete. NEVER wait for the user to tell you to continue if you have more tools to run.\n6. To READ a web page use: open_url(url=\"URL\"). This fetches the page and returns its readable text. Use this for any task that requires information from the internet (articles, docs, search results, weather, etc.).\n7. If open_url returns FAILED with 403 or 429 (bot protection), IMMEDIATELY retry with the site's RSS or Atom feed URL (e.g. /rss, /feed, /rss.xml, /atom.xml). Never tell the user to open a browser — just retry.\n8. To open a URL visibly in the Aire browser window use: open_browser_tab(url=\"URL\"). Use this whenever the user says 'open', 'show', 'navigate to', or wants to see a page.\n9. To check if an app is installed use: execute_command with command=\"where appname\" (fast) or \"winget list --name appname\" (detailed). To list all installed apps use \"winget list\".\n\nTo call a tool, place EXACTLY ONE of the following at the END of your message:\n\n<tool_call>{\"tool\": \"TOOL_NAME\", ...parameters}</tool_call>\n\nAVAILABLE TOOLS:\n1. File System: list_directory, read_file(path, offset?, length?) — reads up to 100 000 chars; result shows total size and remaining chars so you can loop with increasing offset for large files, write_file(path, content, append?) — append=true adds to end instead of overwriting; use this to write large content in chunks, apply_diff, search_files, search_file_content(directory, pattern, file_pattern?, max_results?), create_directory, delete_file, move_file.\n2. Command Execution: execute_command — launches any app or shell command. read_command_output — reads output from a background command.\n3. Web — background fetch: open_url(url, max_chars?) — silent HTTP fetch, returns plain text. http_request(url, method?, headers?, body?) — full HTTP request with custom method/headers/body, returns raw response.\n4. Web — visible browser: open_browser_tab(url) opens a URL. list_browser_tabs() lists tabs; read_browser_tab(index?) reads rendered content;\n   switch_browser_tab(index), close_browser_tab(index), get_browser_html(index), get_browser_cookies(index).\n   execute_browser_script(script, index?) — run JS in the tab (fill forms, click buttons, extract data — faster than mouse).\n   To open a link on the current page: read_browser_tab(-1) FIRST → find the real URL → open_browser_tab(url=FOUND_URL). NEVER invent a URL.\n5. System Control: take_screenshot, begin_keyboard_session, end_keyboard_session, key_combo, key_press, type_text, begin_mouse_session, end_mouse_session, mouse_move, mouse_click, mouse_double_click, mouse_drag.\n6. System Utilities: show_notification(title, message) — show a Windows desktop notification. get_clipboard() — read clipboard text. set_clipboard(text) — write text to clipboard.\n   get_system_info() — OS/CPU/RAM/disk info. get_running_processes(top_n?, filter?) — list running processes. get_active_window() — title/process of focused window. get_selected_text() — text selected in any app. open_file(path) — open with default app.\n   remember(key, value) — persist a fact across conversations (pass empty value to delete). recall(key?) — retrieve a stored fact (empty key lists all). set_reminder(message, delay_minutes) — fire a notification after a delay.\n7. Email: read_emails(account?, count?) — read recent emails. search_emails(query, account?) — search by keyword. send_email(to, subject, body, account?) — send an email. reply_to_email(message_id, body, account?) — reply to a thread.\n8. Agent / Task flow: new_task(task) — start a new subtask. attempt_completion(result) — signal the task is done. ask_followup_question(question) — ask the user for clarification. skill(name) — run a named skill (e.g. 'list_tools' lists all available tools). switch_mode(mode) — switch assistant mode. update_todo_list(todos) — update the to-do list. show_image(path_or_url, caption?) — display an image in the chat.\n9. Model switching: switch_model(model_name, reason, direction) — switch to a different AI model. direction: \"up\" (need more capability), \"down\" (scale back), \"lateral\" (change provider). model_name must exactly match an entry from the model list appended to this prompt.\n\nSCRIPTING RULE:\n- When a task requires writing any script, program, or block of code (PowerShell, Python, batch, etc.), ALWAYS use write_file to save it to a temp file FIRST, then execute_command to run it.\n- NEVER output large code blocks as plain text in the chat — the chat window has limited capacity and the response will be cut off or break the conversation.\n- Example sequence: write_file(path=\"C:/Temp/task.ps1\", content=\"...\") → execute_command(command=\"powershell -File C:/Temp/task.ps1\").\n- If the script is too large for one write_file call, write it in parts: write_file(path, firstPart) → write_file(path, nextPart, append=true) → ... → execute_command.\n\nLARGE FILE RULE:\n- read_file returns at most 100 000 chars per call. The result header tells you the total file size and how many chars remain.\n- If more remains, call read_file again with offset=<nextOffset>. Repeat until the result says the read is complete.\n- Example: read_file(path, offset=0) → result says 'Remaining: 45000 — call with offset=100000' → read_file(path, offset=100000).\n\nCRITICAL RULE ABOUT TOOL RESULTS:\n- When a tool result begins with SUCCESS, that specific step succeeded. Check if the task is complete; if not, continue to the next step (e.g., calling begin_keyboard_session after launching an app).\n- Only stop when the user's entire request is fully satisfied.\n\nEXAMPLES:\nUser: 'Open notepad and write hello world'\nYou: I'll open notepad and write that for you.\n     <tool_call>{\"tool\": \"execute_command\", \"command\": \"notepad\"}</tool_call>\nTool result: SUCCESS: 'notepad' opened (PID: 5678).\nYou: I've opened Notepad. Now, I'll start a keyboard session to type the text.\n     <tool_call>{\"tool\": \"begin_keyboard_session\", \"duration_minutes\": 5}</tool_call>\nTool result: SUCCESS: Keyboard session started.\nYou: Typing the text...\n     <tool_call>{\"tool\": \"type_text\", \"text\": \"hello world\"}</tool_call>\n\nUser: 'open the Shows link on this page' (browser already open)\nYou: I'll read the current tab to find the Shows link.\n     <tool_call>{\"tool\": \"read_browser_tab\", \"index\": -1}</tool_call>\nTool result: (page text containing '... Shows https://example.com/shows ...')\nYou: Found it. Opening the Shows page.\n     <tool_call>{\"tool\": \"open_browser_tab\", \"url\": \"https://example.com/shows\"}</tool_call>\n\nKEYBOARD-FIRST RULE — CRITICAL:\nALWAYS try keyboard shortcuts before touching the mouse. Mouse coordinates are fragile.\nOnly request begin_mouse_session when there is absolutely no keyboard alternative.\n\nWORKFLOW — follow exactly:\n1. To open an app and type: call execute_command → (after success) call begin_keyboard_session → (after success) call type_text/key_combo → end_keyboard_session.\n2. ALWAYS call take_screenshot after opening an app or finishing a type task to verify progress.\n3. Chains calls one at a time. Do NOT stop until the user's entire request is satisfied.\n4. Valid key names: Enter, Tab, Escape, Backspace, Delete, Home, End, PageUp, PageDown, Left, Up, Right, Down, Ctrl, Alt, Shift, Win, Space, F1-F12, or any single character.\n\nADDITIONAL RULES:\n- Use Windows-style paths (e.g. C:/Users/username/Documents).\n- Put the tool call at the very end of your message.\n- Always read a file before editing it.\n- To READ a web page: open_url(url=\"URL\"). Returns the plain text. Ideal for looking things up, reading docs, news, Wikipedia, etc.\n- If open_url fails with 403/429: retry with the site's RSS feed (/rss or /feed). Do NOT tell the user to open a browser.\n- To open a URL visibly in the Aire browser: open_browser_tab(url=\"URL\").\n- To check if an app is installed: execute_command with command=\"where appname\". To list all installed apps: execute_command with command=\"winget list\".\n- REMEMBER: You CAN run commands, open applications, fetch web pages, and interact with the system. The user will approve each operation before it executes."
            },
            new ChatMessage
            {
                Role = "user",
                Content = userMessage
            }
        };
    }

    private static string BuildOpenGimpPrompt()
    {
        string fullPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..")); string text = Path.Combine(fullPath, "aire.png");
        return "Open the file \"" + text + "\" with gimp";
    }

    private static void AssertToolCallPresent(AiResponse response, string expectedTool, string expectedCommandFragment)
    {
        Assert.True(response.IsSuccess, "Provider returned error: " + response.ErrorMessage);
        Assert.Contains(expectedTool, response.Content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(expectedCommandFragment, response.Content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task DeepSeek_OpenGimpRequest_GeneratesExecuteCommandToolCall()
    {
        if (LiveProvidersEnabled)
        {
            IAiProvider provider = GetProvider("DeepSeek");
            if (provider != null)
            {
                AssertToolCallPresent(await provider.SendChatAsync(BuildMessages(BuildOpenGimpPrompt())), "execute_command", "gimp");
            }
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task DeepSeek_OpenGimpRequest_CommandContainsFilePath()
    {
        if (LiveProvidersEnabled)
        {
            IAiProvider provider = GetProvider("DeepSeek");
            if (provider != null)
            {
                AiResponse response = await provider.SendChatAsync(BuildMessages(BuildOpenGimpPrompt()));
                Assert.True(response.IsSuccess, response.ErrorMessage);
                Assert.Contains("aire.png", response.Content, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Inception_OpenGimpRequest_GeneratesExecuteCommandToolCall()
    {
        if (LiveProvidersEnabled)
        {
            IAiProvider provider = GetProvider("Inception");
            if (provider != null)
            {
                AssertToolCallPresent(await provider.SendChatAsync(BuildMessages(BuildOpenGimpPrompt())), "execute_command", "gimp");
            }
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Inception_OpenGimpRequest_CommandContainsFilePath()
    {
        if (LiveProvidersEnabled)
        {
            IAiProvider provider = GetProvider("Inception");
            if (provider != null)
            {
                AiResponse response = await provider.SendChatAsync(BuildMessages(BuildOpenGimpPrompt()));
                Assert.True(response.IsSuccess, response.ErrorMessage);
                Assert.Contains("aire.png", response.Content, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Claude_OpenGimpRequest_GeneratesExecuteCommandToolCall()
    {
        if (LiveProvidersEnabled)
        {
            IAiProvider provider = GetProvider("Anthropic");
            if (provider != null)
            {
                AssertToolCallPresent(await provider.SendChatAsync(BuildMessages(BuildOpenGimpPrompt())), "execute_command", "gimp");
            }
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Claude_OpenGimpRequest_CommandContainsFilePath()
    {
        if (LiveProvidersEnabled)
        {
            IAiProvider provider = GetProvider("Anthropic");
            if (provider != null)
            {
                AiResponse response = await provider.SendChatAsync(BuildMessages(BuildOpenGimpPrompt()));
                Assert.True(response.IsSuccess, response.ErrorMessage);
                Assert.Contains("aire.png", response.Content, StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}
