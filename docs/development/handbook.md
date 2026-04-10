# Aire Development Handbook

This handbook covers everything needed to work on the Aire codebase: build
instructions, architecture, conventions, testing, and the provider/tool systems.

---

## Build and Run

**Prerequisites:** .NET 10 SDK, WebView2 Runtime (ships with Windows 11).

```powershell
# Build (single-threaded recommended)
dotnet build .\aire.sln -m:1

# Run
dotnet run --project .\Aire\Aire.csproj

# Test
dotnet test .\Aire.Tests\Aire.Tests.csproj --no-build
```

Optional environment variable gates for extended tests:

| Variable | Effect |
|---|---|
| `AIRE_RUN_LIVE_PROVIDER_TESTS=1` | Runs tests against real configured providers from the local Aire database |
| `AIRE_RUN_CONNECTIVITY_TESTS=1` | Validates connectivity against enabled providers |
| `AIRE_TEST_DEEPSEEK_API_KEY=...` | Enables the DeepSeek token-usage integration test |
| `AIRE_GOOGLE_CLIENT_ID=...` | Google OAuth client ID for Gmail integration tests |
| `AIRE_GOOGLE_CLIENT_SECRET=...` | Google OAuth client secret |

---

## Project Structure

```
aire.sln
  Aire/                  WPF desktop application (.NET 10, Windows 10.0.17763.0+)
  Aire.Core/             Shared domain: providers, tools, data models (.NET 10)
  Aire.Tests/            xUnit test suite
  Aire.Setup/            First-run accessibility and preference bootstrapper
  Aire.Screenshots/      Automated help screenshot generation
  Aire.Installer/        WiX-based MSI installer
```

### Aire/ (desktop app)

```
Application/             Application service layer (business logic, orchestration)
  Abstractions/            Repository and interface contracts
  Api/                     Local API request/response shaping
  Chat/                    Chat orchestration services
  Mcp/                     Model Context Protocol integration
  Providers/               Provider selection and management
  Settings/                Settings persistence layer
  Startup/                 Application initialization
  Tools/                   Tool approval and control services
  Connections/             Email/integration management
Services/                Infrastructure services
  Providers/               Provider-specific adapters
  Tools/                   Tool execution (file, system, browser, email)
  Workflows/               Tool and chat workflow orchestration
  Email/                   Email account services
  SpeechRecognition/       Audio/voice services
  Policies/                Tool auto-accept policies
  Ollama/                  Ollama-specific management
Data/                    SQLite database and schema
  Database/                DatabaseService partials
Providers/               UI-layer provider catalog and registry
UI/                      WPF windows and controls
  MainWindow/              Main chat window (decomposed into partial classes)
  SettingsWindow/          Settings UI
  OnboardingWindow/        Provider setup wizard
  Controls/                Reusable WPF controls
```

### Aire.Core/ (shared library)

```
Providers/               Provider interfaces and implementations
  IAiProvider.cs             Core provider contract and BaseAiProvider
  OpenAiProvider.cs          OpenAI / compatible APIs
  GoogleAiProvider.cs        Google Gemini
  OllamaProvider.cs          Local Ollama
  ClaudeCodeProvider.cs      Claude Code CLI bridge
  MistralProvider.cs         Mistral
  ... (12+ total)
  SharedToolDefinitions*.cs  Canonical tool catalog (partials by category)
Domain/                  Domain models
  Providers/               ToolIntent, ProviderContracts
  Tools/                   ToolCategoryContracts, ToolCategoryOption
Services/                Core services
  Tools/                   FileSystemService, CommandToolService, etc.
Data/                    Data models (Provider, Conversation, Message, etc.)
  Models/                  Embedded JSON model catalogs per provider
```

### Key Namespaces

| Namespace | Purpose |
|---|---|
| `Aire` | Main app root |
| `Aire.AppLayer` | Application service layer |
| `Aire.AppLayer.Abstractions` | Repository contracts (IProviderRepository, etc.) |
| `Aire.AppLayer.Chat` | Chat-specific orchestration |
| `Aire.AppLayer.Api` | Local API services |
| `Aire.Data` | Data models and DatabaseService |
| `Aire.Domain.Providers` | Provider domain (ToolIntent, ProviderContracts) |
| `Aire.Domain.Tools` | Tool domain (ToolCategoryCatalog) |
| `Aire.Providers` | Provider registry and catalog |
| `Aire.Services` | Infrastructure services |
| `Aire.Bootstrap` | Application initialization |

---

## Architecture

### Layering

```
UI (Aire/UI/)
  ↓ calls
Application Services (Aire/Application/)
  ↓ calls
Domain / Infrastructure (Aire.Core/, Aire/Data/, Aire/Services/)
```

- **UI layer**: WPF windows and controls. Calls application services, never
  manipulates data directly. Large windows are decomposed into partial classes.
- **Application layer**: Sealed service classes that orchestrate business logic.
  Depend on repository abstractions, never on WPF.
- **Domain/Infrastructure layer**: Provider contracts, tool definitions, data
  models, database access. No dependencies on the application or UI layers.

### Application Services

Services in `Aire/Application/` follow a consistent pattern:

- Sealed classes (not meant for inheritance)
- Constructor injection of repository/abstraction dependencies
- Public async methods returning `Task` or `Task<T>`
- Single responsibility: one business concept per service

Key services:

| Service | Responsibility |
|---|---|
| `ChatSessionApplicationService` | Active session state, provider selection, conversation loading |
| `ChatTurnApplicationService` | Turn processing: text responses, tool execution, image generation |
| `ChatInteractionApplicationService` | User interaction coordination |
| `ConversationApplicationService` | Conversation lifecycle (create, delete, search) |
| `LocalApiApplicationService` | Local API request/response normalization |

### UI Decomposition (Partial Classes)

MainWindow is decomposed into multiple files to keep each focused:

| File | Responsibility |
|---|---|
| `MainWindow.xaml.cs` | XAML code-behind |
| `MainWindow.Shell.cs` | Control setup, event wiring |
| `MainWindow.State.cs` | State management (active conversation, provider) |
| `MainWindow.WindowState.cs` | Window lifecycle (minimize, close, restore) |
| `MainWindow.ViewState.cs` | UI view state (theme, layout) |

Other large windows (SettingsWindow, OnboardingWindow, HelpWindow, ImageViewerWindow)
follow the same partial-class decomposition pattern.

---

## Provider System

### IAiProvider Interface

```csharp
public interface IAiProvider
{
    string ProviderType { get; }        // "OpenAI", "GoogleAI", "Ollama", etc.
    string DisplayName { get; }         // "OpenAI (ChatGPT)"
    ProviderCapabilities Capabilities { get; }
    ToolCallMode ToolCallMode { get; }
    ToolOutputFormat ToolOutputFormat { get; }

    void Initialize(ProviderConfig config);
    Task<AiResponse> SendChatAsync(IEnumerable<ChatMessage> messages, CancellationToken ct);
    IAsyncEnumerable<string> StreamChatAsync(IEnumerable<ChatMessage> messages, CancellationToken ct);
    Task<ProviderValidationResult> ValidateConfigurationAsync(CancellationToken ct);
    Task<TokenUsage?> GetTokenUsageAsync(CancellationToken ct);
    void PrepareForCapabilityTesting();
    void SetToolsEnabled(bool enabled);
    void SetEnabledToolCategories(IEnumerable<string>? categories);
}
```

### ProviderConfig

```csharp
public class ProviderConfig
{
    public string ApiKey { get; set; }
    public string? BaseUrl { get; set; }
    public string Model { get; set; }
    public double Temperature { get; set; } = 0.7;
    public int MaxTokens { get; set; } = 16384;
    public int TimeoutMinutes { get; set; } = 5;
    public List<string>? ModelCapabilities { get; set; }   // "tools", "vision", "imagegeneration"
    public List<string>? EnabledToolCategories { get; set; }
    public bool SkipNativeTools { get; set; }
}
```

### Key Enums

**ProviderCapabilities** (flags): TextChat, Streaming, ImageInput, ToolCalling, SystemPrompt

**ToolCallMode**: NativeFunctionCalling, TextBased, Unsupported

**ToolOutputFormat**: NativeToolCalls, Hermes, React, AireText

### Provider Lifecycle

1. Load `Provider` entity from database (name, type, API key, base URL, model, timeout, capabilities)
2. Create `IAiProvider` instance via adapter/factory
3. Call `provider.Initialize(config)` with a normalized `ProviderConfig`
4. Use provider for chat, streaming, or validation

### Adding a New Provider

1. Create `YourProvider.cs` in `Aire.Core/Providers/` extending `BaseAiProvider`
2. Override `SendChatAsync()` to translate `ChatMessage` into the provider's API format
3. Override `StreamChatAsync()` if streaming is supported
4. Override `ValidateConfigurationAsync()` with provider-specific validation
5. Add a model catalog JSON in `Aire.Core/Data/Models/models-yourprovider.json`
6. Register the provider type in `ProviderRegistry` and `ProviderIdentityCatalog`
7. Add an adapter in `Aire/Services/Providers/` if the provider needs custom orchestration
8. Add tests in `Aire.Tests/Providers/`

---

## Tool System

### Tool Catalog

All tools are defined once in `SharedToolDefinitions` (Aire.Core/Providers/),
partitioned by category across partial classes:

| Partial | Categories |
|---|---|
| `SharedToolDefinitions.FileSystem.cs` | read_file, write_file, list_files, etc. |
| `SharedToolDefinitions.Browser.cs` | navigate_to, get_page_source, etc. |
| `SharedToolDefinitions.Email.cs` | read_emails, send_email, etc. |
| `SharedToolDefinitions.SystemAndEmail.cs` | execute_command, get_processes, open_app, etc. |
| `SharedToolDefinitions.InputAndAgent.cs` | move_mouse, click, type_text, etc. |

Each tool has: name, description, category, parameters (JSON schema), required fields.

### Tool Categories

Defined in `ToolCategoryContracts`:

| Category | Label | Description |
|---|---|---|
| `filesystem` | Files | Read and modify local files and folders |
| `browser` | Browser | Inspect and control browser tabs and pages |
| `agent` | Agent | Planning and follow-up |
| `mouse` | Mouse | Mouse control |
| `keyboard` | Keyboard | Keyboard input |
| `system` | System | System inspection and commands |
| `email` | Email | Email operations |

Categories are gated by model capability tags and can be enabled/disabled per
conversation by the user.

### Tool Format Conversion

`SharedToolDefinitions` provides format adapters for each provider:

```csharp
public static IReadOnlyList<object> ToOpenAiFunctions(...)
public static IReadOnlyList<object> ToAnthropicTools(...)
public static IReadOnlyList<object> ToGeminiFunctionDeclarations(...)
public static List<object> ToOllamaTools(...)
```

### Tool Execution Flow

1. Model generates a tool call in its response
2. `ToolIntent` is parsed (canonical tool name + JSON parameters)
3. Human-readable description is generated for the approval UI
4. User approves or denies
5. `ToolExecutionService` dispatches to the appropriate domain service
6. `ToolExecutionResult` is returned and embedded in the next model message

### Tool Execution Routing

`ToolExecutionService` (Aire/Services/) routes tool calls to:

| Domain Service | Tools |
|---|---|
| `FileSystemService` | File and folder operations |
| `CommandToolService` | execute_command |
| `WebToolService` | web_fetch, web_search |
| `BrowserToolService` | Browser control |
| `InputToolService` | Mouse, keyboard, screenshot |
| `SystemToolService` | System inspection |
| `MemoryToolService` | Internal memory |
| `EmailToolService` | Email operations |
| `McpManager` | MCP server tools |

---

## Database

### Storage

- Default path: `%LOCALAPPDATA%\Aire\aire.db`
- Engine: SQLite via `Microsoft.Data.Sqlite`
- Single connection per `DatabaseService` instance

### Tables

| Table | Purpose |
|---|---|
| `Providers` | Provider configurations (type, API key, base URL, model, timeout, capabilities) |
| `Conversations` | Conversation metadata (title, provider, assistant mode, timestamps) |
| `Messages` | Messages with role, content, images, attachments |
| `Settings` | Key-value app settings |
| `EmailAccounts` | Email integration with encrypted OAuth tokens |
| `McpServers` | MCP server configurations |

### Encryption

API keys and OAuth tokens are encrypted using DPAPI (`System.Security.Cryptography.ProtectedData`)
with user-scoped protection and an entropy salt (`"Aire-SecureStorage-v1"`). Encryption
is idempotent (already-encrypted values are detected and returned as-is).

### Migration Pattern

`DatabaseService.InitializeAsync()` runs all migrations in order. Each migration
checks if it has already been applied and only adds schema or data when needed:

```csharp
public async Task InitializeAsync()
{
    await CreateTablesAsync();
    await MigrateProviderTypesAsync();
    await MigrateProviderBaseUrlsAsync();
    await MigrateAddSortOrderAsync();
    // ... 10+ migrations in sequence
    await SeedDefaultProvidersAsync();
}
```

### Repository Interfaces

`DatabaseService` implements all three repository interfaces via partial classes:

| Interface | Methods |
|---|---|
| `IProviderRepository` | GetProvidersAsync, InsertProviderAsync, UpdateProviderAsync, DeleteProviderAsync |
| `IConversationRepository` | CreateConversationAsync, GetConversationAsync, ListConversationsAsync, SaveMessageAsync, GetMessagesAsync, DeleteConversationAsync |
| `ISettingsRepository` | GetSettingAsync, SetSettingAsync |

---

## Conventions

### Naming

| Element | Convention | Example |
|---|---|---|
| Classes | PascalCase | `ChatSessionApplicationService` |
| Async methods | PascalCase + `Async` suffix | `SendChatAsync` |
| Properties | PascalCase | `DisplayName` |
| Parameters | camelCase | `providerId` |
| Private fields | _camelCase | `_connection` |
| Constants | PascalCase | `DefaultTimeoutMinutes` |

### Async Patterns

- Use `async Task` / `async Task<T>` for all async work.
- Avoid `async void` (only in WPF event handlers).
- Use `ConfigureAwait(false)` in library code (Aire.Core).
- Accept `CancellationToken cancellationToken = default` on long-running operations.
- Use `[EnumeratorCancellation]` on `IAsyncEnumerable<T>` parameters.

### Null Handling

- Nullable reference types are enabled project-wide (`<Nullable>enable</Nullable>`).
- Use `string?` for nullable, `string` for non-null.
- Prefer `string.IsNullOrWhiteSpace()` over `== null`.
- Use `??` and `??=` operators freely.

### Error Handling

- Catch specific exceptions. Avoid bare `catch` blocks.
- Return error-indicating types (`AiResponse` with `IsSuccess = false`,
  `ProviderValidationResult.Fail("message")`).
- Keep user-facing error messages actionable.
- Log with `Debug.WriteLine` (migration to structured logging is planned).

### Resource Management

- Implement `IDisposable` for classes owning unmanaged resources.
- Use `using` statements for scoped resources.
- Check `_disposed` before disposing.

### Serialization

- Use `System.Text.Json` (not Newtonsoft).
- Case-insensitive JSON parsing for flexibility with provider APIs.

### Formatting

From `.editorconfig`:

- 4-space indentation
- CRLF line endings
- New line before open braces (Allman style)
- No `this.` qualification
- Predefined types (`int`, `string`) over BCL types (`Int32`, `String`)
- No space after cast: `(int)value`

---

## Testing

### Framework

xUnit 2.9 with coverlet for code coverage.

### Test Structure

```csharp
public class DatabaseServiceTests : IAsyncLifetime, IDisposable
{
    private readonly string _dbPath;
    private readonly DatabaseService _db;

    public DatabaseServiceTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"aire_test_{Guid.NewGuid():N}.db");
        _db = new DatabaseService(_dbPath);
    }

    public async Task InitializeAsync() => await _db.InitializeAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    public void Dispose()
    {
        _db.Dispose();
        SqliteConnection.ClearAllPools();
        try { File.Delete(_dbPath); } catch { }
    }

    [Fact]
    public async Task InitializeAsync_SeedsDefaultProviders()
    {
        var providers = await _db.GetProvidersAsync();
        Assert.NotEmpty(providers);
    }
}
```

### Patterns

- Use `IAsyncLifetime` for async setup/teardown.
- Use temp paths with `Guid` for test isolation.
- Clean up after tests (delete temp databases, clear pools).
- Name tests `MethodName_Scenario_ExpectedResult`.
- Use `[Collection("...")]` to disable parallelization when tests share state.

### Stubs

Prefer inline stub classes implementing `IAiProvider` or other interfaces:

```csharp
private sealed class StubProvider(Func<IReadOnlyList<ChatMessage>, AiResponse> responder) : IAiProvider
{
    public string ProviderType => "Stub";
    public Task<AiResponse> SendChatAsync(IEnumerable<ChatMessage> messages, CancellationToken ct)
        => Task.FromResult(responder(messages.ToList()));
    // ... other interface members
}
```

### Test Organization

| Directory | Scope |
|---|---|
| `Aire.Tests/Core/` | Domain logic, tool parsing, model catalog |
| `Aire.Tests/Providers/` | Provider-specific tests |
| `Aire.Tests/Services/` | Application and infrastructure service tests |
| `Aire.Tests/UI/` | WPF window and control tests |
| `Aire.Tests/Capabilities/` | Capability test runner tests |

---

## CI/CD

### CI Pipeline (`.github/workflows/ci.yml`)

Runs on `windows-latest` with .NET 10:

1. Checkout, setup .NET SDK
2. Restore: `dotnet restore aire.sln`
3. Build: `dotnet build aire.sln --no-restore -c Release -m:1`
4. Test: `dotnet test --no-build -c Release` with code coverage collection
5. Parse results and generate GitHub summary (test count, pass rate, coverage)

Supports scoped test runs via `workflow_dispatch` input: `all`, `core`, `providers`,
`services`, `ui`.

### Release Pipeline (`.github/workflows/release.yml`)

Builds the MSI installer and publishes to GitHub Releases.

---

## App Lifecycle

### Single Instance

The app enforces single instance via a Windows Mutex:

```csharp
_instanceMutex = new Mutex(true, "Aire_SingleInstance_v1", out bool isFirstInstance);
if (!isFirstInstance)
{
    EventWaitHandle.OpenExisting("Aire_Activate_v1").Set();
    Shutdown(0);
}
```

### Startup Sequence

1. `App.OnStartup` acquires the single-instance mutex
2. Creates `DatabaseService` and calls `InitializeAsync()` (runs all migrations)
3. Creates `MainWindow` (hidden) and `TrayIconService`
4. Tray icon positions the window above the taskbar when shown

### Local API

Loopback-only HTTP listener on `127.0.0.1:51234`. Provides:

- Send messages
- Switch providers and models
- Inspect app state
- List top-level windows
- Select a window for the current session
- Capture the selected window as a PNG path or base64 payload

Intended for trusted local automation and AI agent integration.

---

## AI Contributor Tools

### Local API

AIs can interact with the running app via `http://127.0.0.1:51234` to send
messages, switch providers, inspect state, select top-level windows, and capture
the selected window without a human in the loop.

Recommended flow for autonomous use:

1. Read the local API token from `Aire/appstate_strings.json`.
2. Call `list_windows` to discover the current top-level windows.
3. Call `select_window` with a `windowId`, or use `show_main_window` and then
   `select_window` for the Aire window.
4. Call `capture_selected_window` to save a PNG to disk, or `capture_window`
   with `returnBase64=true` to get the image inline.

Useful methods:

- `get_state`
- `list_windows`
- `get_selected_window`
- `select_window`
- `capture_window`
- `capture_selected_window`

### Screenshot Automation

The `Aire.Screenshots` project captures repeatable screenshots of the running app
and shares the same top-level window capture path that the local API uses:

```powershell
dotnet run --project .\Aire.Screenshots\Aire.Screenshots.csproj -- run-plan --plan ".\Aire.Screenshots\help-assets-plan.json"
```

Screenshots are written to `Aire/Assets/Help/{locale}/` and can be committed
directly. AIs can use this to verify UI changes visually.

When the goal is autonomous agent behavior rather than docs capture, prefer the
local API methods above so the agent can reuse the same window-selection and
capture flow without depending on the separate automation runner.

---

## Further Reading

- [Development roadmap](./roadmap.md)
- [Project analysis by Opus 4.6 (2026-04-09)](./project-analysis-opus-4.6-2026-04-09.md)
- [CONTRIBUTING.md](../../CONTRIBUTING.md)
- [SECURITY.md](../../SECURITY.md)
