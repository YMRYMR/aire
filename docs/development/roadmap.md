# Aire Roadmap

This is the single living plan for the remaining engineering work in Aire.
Completed task briefs and old refactor notes are intentionally removed so the
docs stay short and current.

Status legend: `[ ]` not started · `[~]` in progress · `[x]` done

## 1. Product Reliability

### 1.1 z.ai provider polish `[x]`
### 1.2 Codex provider polish `[x]`
### 1.3 Claude Code provider polish `[x]`
### 1.4 Main chat reliability `[x]`
### 1.5 Settings and onboarding reliability `[x]`

## 2. Architecture Cleanup

### 2.1 Finish window decomposition `[~]`
- Keep extracting logic from large window classes into small, testable partials or coordinators.

### 2.2 Strengthen application-service tests `[x]`
- 1220 non-UI tests passing, covering all application services.

### 2.3 Reduce test fragility `[x]`

### 2.4 Keep layering honest `[x]`
- `MainWindowCompositionRoot` in `Aire.Bootstrap` namespace wired into `App.xaml.cs`.
- MainWindow receives pre-built services via constructor injection.
- Service construction centralized; fields remain on MainWindow for backward compat with partial classes.

### 2.5 Structured logging `[x]`
### 2.6 Fix resource and event leaks `[x]`

### 2.7 Token-Estimation Accuracy `[x]`
- Anthropic, OpenAI, Google, and character-based token estimators with dedicated test coverage.

### 2.8 Consolidate test infrastructure `[x]`
### 2.9 Fix known dependency vulnerability `[x]`

## 3. Quality Bar

### 3.1 Coverage on meaningful paths `[x]`
- All 19 previously untested services now have dedicated test coverage.
- 1220 non-UI tests passing including: `SwitchModelApplicationService` (11), `ConversationExportService` (9),
  `PromptTemplateService` (9), `ContextInjectionToolService` (15), `ConversationBranching` (8).

### 3.2 Release readiness `[ ]`

### 3.3 Human-readable errors `[x]`
- Provider errors are descriptive and sanitized via `ProviderErrorClassifier`.
- Network error sanitization strips internal addresses.

## 4. User-Facing Features (Prioritized)

### 4.1 Keyboard-first UX `[x]`
- Global hotkey (Alt+Space) to toggle window
- Ctrl+K command palette
- Keyboard navigation for dialogs (Enter, Escape, Left/Right arrows)
- Global tool approval shortcuts (Enter=approve, Escape=deny)

### 4.2 Conversation export `[x]`
- Export to Markdown via sidebar context menu and command palette
- `ConversationExportService` with provider info, message timestamps, image notes, tool result cleanup
- 9 dedicated tests

### 4.3 Cost tracking dashboard `[x]`
- Full dashboard in Settings > Usage tab with per-provider breakdown, trend charts, live quota, estimated spend, currency conversion.

### 4.4 Conversation branching / forking `[x]`
- Right-click any message → "Branch from here" creates new conversation with messages up to that point
- `BranchConversationAsync` DB method with ID-based branch point
- `DbMessageId` on ChatMessage, `MessageId` on TranscriptEntry
- Context menu in MessageListItemControl
- 8 dedicated tests

### 4.5 Prompt templates / snippets `[~]`
- User-defined prompt templates with shortcut keys `PromptTemplateService`
- Parameterized templates with `{{placeholder}}` substitution
- Accessible from Ctrl+K command palette
- 9 dedicated tests
- Remaining: template management UI in settings

### 4.6 Multi-model comparison `[ ]`
### 4.7 Provider-returned images `[ ]`
### 4.8 Better context controls `[ ]`
### 4.9 Cleaner tool categories `[ ]`
### 4.10 Better onboarding defaults `[ ]`

## 5. AI-Facing Features (Prioritized)

### 5.1 Agent mode `[x]`
- Auto-approve tool chains without per-call approval `AgentModeService`
- Configurable scope (tool categories) and token budget
- Session-level budget tracking with auto-stop
- Budget configuration dialog `AgentModeConfigDialog`
- Visual budget indicator in composer (live token banner with stop button)
- Toggle button in composer
- Token usage recorded after each AI turn
- Agent mode check in tool approval flow

### 5.2 Structured context injection `[x]`
- `request_context` tool for AI to request context before responding
- Supports: clipboard, environment, datetime, file (text files with binary skip), URL (HTTP fetch with timeout)
- Wired into `ToolExecutionService` dispatch
- 15 dedicated tests

### 5.3 Tool result caching `[ ]`
### 5.4 Persistent AI memory per conversation `[ ]`
### 5.5 Workflow chains `[ ]`
### 5.6 Local RAG integration `[ ]`

### 5.7 Better automation ergonomics `[~]`
### 5.8 Plugin system `[ ]`

## 6. Operating Rules

1. Do not split architecture and behavior rewrites into one large pass.
2. Keep each change buildable and testable on its own.
3. If a change affects user-visible behavior, update the docs and screenshots in the same branch.
4. Prefer removing old plan files once their content has been merged into this roadmap.
5. If a task becomes obsolete, delete it instead of letting it linger as a stale markdown note.

## 7. Analysis Reports

- [Project analysis by Opus 4.6 (2026-04-09)](./project-analysis-opus-4.6-2026-04-09.md)
- [GLM-5.1 evaluation (2026-04-11)](./project-analysis-glm-5.1-2026-04-11.md) *(to be created)*

## 8. Sprint Status (2026-04-13)

Work is happening on branch `glm/aire`. Test suite: **1220 tests, 0 failures**.

| Priority | Task | Roadmap Ref | Status |
|----------|------|-------------|--------|
| P0 | Fix NU1903 `Microsoft.Bcl.Memory` vulnerability | 2.9 | `[x]` |
| P0 | Consolidate `SimpleJsonServer` into shared test utility | 2.8 | `[x]` |
| P1 | Decouple `LocalApiService` from `MainWindow` via `IApiCommandHandler` | 2.4 | `[x]` |
| P1 | Extract MainWindow composition root into `App.xaml.cs` | 2.4 | `[x]` |
| P2 | Cover all Application services (was 19 untested) | 3.1 | `[x]` |
| P2 | Human-readable errors for provider failures | 3.3 | `[x]` |
| P3 | Keyboard-first UX (global hotkey, command palette, dialog nav, tool approval) | 4.1 | `[x]` |
| P3 | Agent mode (auto-approve + budget + config dialog + indicator) | 5.1 | `[x]` |
| P3 | Configurable local API port | 2.4 | `[x]` |
| P3 | Conversation export (Markdown) | 4.2 | `[x]` |
| P3 | Cost tracking dashboard | 4.3 | `[x]` |
| P3 | Prompt templates with shortcut and placeholders | 4.5 | `[~]` |
| P3 | Structured context injection (clipboard, env, datetime, file, URL) | 5.2 | `[x]` |
| P4 | Conversation branching / forking | 4.4 | `[x]` |
| P4 | Token estimation accuracy | 2.7 | `[x]` |
