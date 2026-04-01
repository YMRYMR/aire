# Aire — Master Improvement Plan

This document tracks every identified code-quality and architecture issue, ordered by
priority. Each item has a clear acceptance criterion so work can be resumed at any time.

Status legend: `[ ]` not started · `[x]` done · `[~]` in progress

---

## 1. Security (must fix before public release)

### 1.1 Remove hardcoded OAuth credentials  `[x]`
**File:** `Aire/Services/Email/GoogleOAuthConfig.cs`
**Fix applied:** `ClientId` and `ClientSecret` are now `public static string` properties
reading from `AIRE_GOOGLE_CLIENT_ID` / `AIRE_GOOGLE_CLIENT_SECRET` env vars.
**Tests updated:** `Aire.Tests/Providers/GoogleOAuthConfigTests.cs`.

### 1.2 Verify no other secrets in source  `[x]`
Grepped entire solution. No real credentials found:
- `"claude.ai-session"` — intentional placeholder for ClaudeWeb auth, not a secret.
- `TrustedClientToken` in `EdgeTtsService.cs` — public/documented Edge TTS token.
- All test `sk-*` values are explicit placeholders.
- `LocalApiService.Port = 51234` — only port literal; no raw occurrences elsewhere.

---

## 2. Critical — Architecture

### 2.1 Extract `IAppState` interface  `[x]`
**Files created:**
- `Aire/Services/IAppState.cs` — interface mirroring every public method/event
- `Aire/Services/AppStateImpl.cs` — adapter delegating to the `AppState` static class
  with a `static readonly Instance` singleton for non-DI callers

The static `AppState` class is intentionally left unchanged as the persistence layer.
New code that needs testability can accept `IAppState`; a stub can be injected in tests.

**Next step (deferred):** Wire `IAppState` via DI in `App.xaml.cs` and update
`MainWindow` / services that directly call `AppState` to accept `IAppState` instead.

### 2.2 Deduplicate `ProviderFactory` switch  `[x]`
**Fix applied:** Single `_wpfProviderFactories` dictionary in `ProviderFactory.cs`;
both `CreateProvider` and `GetMetadata` delegate to it.

### 2.3 Eliminate direct service access from `MainWindow`  `[x]`
All `_databaseService` direct calls in `MainWindow` and `SettingsWindow` are now
routed through application-layer services:

**New interfaces:** `IDatabaseInitializer`, `IMcpConfigRepository` (CRUD),
`IEmailAccountRepository` (CRUD) — all implemented by `DatabaseService`.

**New app services:**
- `McpStartupApplicationService` — loads and starts MCP servers at startup
- `McpConfigApplicationService` — MCP CRUD for SettingsWindow
- `EmailAccountApplicationService` — email CRUD for SettingsWindow
- `AppSettingsApplicationService` — general key/value settings for SettingsWindow

**Remaining:** `_databaseService.InitializeAsync()` in both `MainWindow.Startup.cs`
and `SettingsWindow.Startup.cs` — left calling the concrete service directly since
initialization is an infrastructure concern with no meaningful abstraction benefit.

### 2.4 Begin `MainWindow` god-class decomposition  `[x]`
All five coordinators extracted as `private sealed (partial) class` nested inside
`MainWindow` partial classes under `Aire/UI/MainWindow/Coordinators/`:

| Coordinator | Files | Lazy accessor |
|---|---|---|
| `VoiceCoordinator` | `MainWindow.VoiceCoordinator.cs` | `VoiceFlow` |
| `ChatCoordinator` | `MainWindow.ChatCoordinator.cs` + `.Send.cs` + `.ToolCalls.cs` | `ChatFlow` |
| `ConversationCoordinator` | `MainWindow.ConversationCoordinator.cs` | `ConversationFlow` |
| `ToolApprovalCoordinator` | `MainWindow.ToolApprovalCoordinator.cs` | `ToolApprovals` |
| `ProviderCoordinator` | `MainWindow.ProviderCoordinator.cs` + `.Availability.cs` + `.TokenUsage.cs` | `ProvidersFlow` |

`MainWindow` holds only the lazy coordinator references; `Chat/MainWindow.AiLoop.*.cs`
are thin shims that delegate into `ChatFlow`.

**Unit tests deferred:** coordinators are `private` nested classes with direct WPF-control
access via `_owner`; isolated tests would require promoting them to `internal` top-level
classes behind a host interface — acceptable future work once the coordinator boundaries
stabilise further.

---

## 3. High — Error Handling

### 3.1 Replace silent `catch { }` with logged catches  `[x]`
**`AppLogger` created** at `Aire/Services/AppLogger.cs` — writes to Debug output and
`%LOCALAPPDATA%\Aire\aire.log`.

**All service / infrastructure catches fixed:**
- `AppState` (SetBool/SetString/LoadAllBools/LoadAllStrings)
- `ChatTurnApplicationService` — screenshot read failure
- `ChatSubmissionWorkflowService` — attached-file read failure
- `ProviderPresentationWorkflowService` — provider metadata tag resolution
- `CodexManagementClient` — npm probe failure
- `WebViewWindow.State` — load/save window position and tabs

**All UI shell catches fixed:**
- `MainWindow.WindowState` — load/save
- `SettingsWindow.WindowState` — load/save
- `HelpWindow.Shell` — load/save
- `MainWindow.ConversationCoordinator` — history image load
- `MainWindow.ToolApprovalCoordinator` — screenshot for tool result
- `MainWindow.ProviderCoordinator.TokenUsage` — token state update
- `SettingsWindow.CapabilityTests` — load/save results
- `OnboardingWindow.Ollama` — duplicate provider check

**Legitimately silent (annotated with comments):**
- `OllamaService.Management.Log()` — logger must never throw
- `OllamaService.Management` SSE parse — partial line, skip and continue
- `ToolFollowUpWorkflowService` JSON fallbacks — try JSON, fall through to text
- `DatabaseService.Schema` `ALTER TABLE` — idiomatic SQLite "add if missing" pattern
- `CodexManagementClient` cancellation kill — best-effort process kill
- Clipboard, URL normalization, JSON string unescaping — UI fallback patterns

### 3.2 Surface configuration errors to the user  `[x]`
**`ProviderValidationResult`** record added to `Aire/Domain/Providers/ProviderContracts.cs`
with `Ok()` / `Fail(string error)` factory methods.

All `ValidateConfigurationAsync` overrides updated to return `ProviderValidationResult`
instead of `bool` (7 provider files + interface + base class).

**Wired into UI:**
- `SettingsWindow.CapabilityTests` — validation runs before smoke test; error shown via toast
- `OnboardingWindow.ProviderSetup.Testing` — validation runs before smoke test; error shown inline

**Tests updated/added:**
- `AppProviderCoverageTests` — assertions on `Error` string for ClaudeAi, ClaudeWeb, Ollama
- `GoogleAiProviderTests`, `OpenAiProviderTests` — `Error` string assertions
- `ApplicationServiceTests` — 3 new `ProviderValidationResult` unit tests
- 11 new direct unit tests for `AppSettingsApplicationService`, `McpConfigApplicationService`, `EmailAccountApplicationService` (fakes only, no DB)

**Quick fixes also applied:**
- 8 nullable `_test*` fields in `MainHeaderControl.xaml.cs` (eliminates CS8618 warnings)
- Duplicate `using Aire.Services` removed from `HelpWindow.Shell.cs`

---

## 4. High — Code Duplication / Naming

### 4.1 Named constant for `LocalApiService` port  `[x]`
`public const int Port = 51234;` already declared. No raw `51234` literals elsewhere.

### 4.2 Named constants for magic values  `[x]`
- `Provider.DefaultTimeoutMinutes = 5` added to `Aire.Core/Data/Provider.cs`
- All 5 `5` literals in `DatabaseService.Providers/Schema` and `SettingsWindow.ProviderForm`
  replaced with `Provider.DefaultTimeoutMinutes`
- `const int maxIterations = 40` was already a named local in `MainWindow.ChatCoordinator.cs`

---

## 5. Medium — Design Smells

### 5.1 Boolean trap in `SetSidebarOpen(bool)`  `[x]`
**Fix applied:** `SetSidebarOpen(bool)` replaced with `OpenSidebar()` / `CloseSidebar()`
in `AppState.cs`; single callsite in `MainWindow.Sidebar.cs` updated; test updated.

### 5.2 `DatabaseService` interface split  `[x]`
`DatabaseService` now implements six narrow interfaces: `IProviderRepository`,
`IConversationRepository`, `ISettingsRepository`, `IDatabaseInitializer`,
`IMcpConfigRepository`, `IEmailAccountRepository`.
All consumers outside the composition root (`MainWindow.Startup`, `SettingsWindow.Startup`)
now declare only the interface they need rather than the concrete `DatabaseService`.

---

## 6. Low — Documentation for Contributors

### 6.1 Architecture overview is current  `[x]`
`docs/architecture/overview.md` and `docs/architecture/developer-map.md` updated to
reflect new `Application/Mcp/`, `Application/Connections/`, and `Application/Settings/`
directories and their MCP/email responsibilities.

### 6.2 CONTRIBUTING.md covers build, test, and provider extension  `[x]`
`CONTRIBUTING.md` already contains build steps, test commands, live-test env vars,
Gmail OAuth setup with env var instructions, and links to all architecture guides.

---

## Recommended next steps

| Step | Item | Status |
|------|------|--------|
| — | 1.1–1.2 Security | ✅ done |
| — | 2.2 ProviderFactory dedup | ✅ done |
| — | 3.1 Logged catches + AppLogger | ✅ done |
| — | 2.1 IAppState interface | ✅ done |
| — | 4.1–4.2 Named constants | ✅ done |
| — | 5.1 Boolean trap | ✅ done |
| — | 3.2 Surface config errors | ✅ done |
| — | 5.2 DatabaseService narrowing | ✅ done |
| — | 2.3 MainWindow → AppLayer routing | ✅ done |
| — | 5.2 DatabaseService narrowing | ✅ done |
| — | 6.1–6.2 Docs | ✅ done |
| — | 2.4 MainWindow decomposition | ✅ done |
