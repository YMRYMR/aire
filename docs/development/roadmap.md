# Aire Roadmap

This is the single living plan for the remaining engineering work in Aire.
Completed task briefs and old refactor notes are intentionally removed so the
docs stay short and current.

Status legend: `[ ]` not started · `[~]` in progress · `[x]` done

## 1. Product Reliability

### 1.1 z.ai provider polish `[x]`
- Verify validation, model loading, tool calling, and onboarding flow against the current provider contract.
- Keep provider-specific errors clear and actionable.

### 1.2 Codex provider polish `[x]`
- Keep the CLI bridge reliable for local use.
- Distinguish missing CLI, missing auth, and runtime failures in user-facing errors.
- Keep the install / onboarding path obvious.

### 1.3 Claude Code provider polish `[x]`
- Keep the CLI bridge working for both Windows and WSL only when the CLI is actually present.
- Keep the provider available in release and debug builds.
- Keep failure messages specific enough to guide setup.

### 1.4 Main chat reliability `[x]`
- Keep message bubbles compact for short text while preserving selection/copy behavior.
- Keep search, scrolling, and attachment rendering reliable.
- Keep streaming and tool-call rendering stable.

### 1.5 Settings and onboarding reliability `[x]`
- Keep provider selection safe when hidden provider types exist in saved data.
- Keep wizard ordering, localization, RTL layout, and provider-specific screens consistent.
- Keep debug-only providers hidden from normal users.

## 2. Architecture Cleanup

### 2.1 Finish window decomposition `[~]`
- Keep extracting logic from large window classes into small, testable partials or coordinators.
- Prefer UI shells that forward to application services instead of owning workflow logic.
- Recent progress: split main-window localization, settings localization, chat image generation, and capability-test result rendering into dedicated partials.

### 2.2 Strengthen application-service tests `[~]`
- Add direct tests for important application services instead of relying only on WPF-backed tests.
- Focus first on chat orchestration, provider workflows, and settings persistence.
- Recent progress: added direct provider-activation workflow tests, provider UI-state tests, and chat-session tests that cover provider persistence, conversation routing, cooldown messaging, token-usage presentation, message persistence, and title updates without the WPF shell.

### 2.3 Reduce test fragility `[x]`
- Remove remaining assumptions that depend on `GetUninitializedObject` or hidden WPF state.
- Prefer test seams that construct the real object graph when feasible.

### 2.4 Keep layering honest `[ ]`
- Preserve the `UI -> Application -> Domain/Infrastructure` direction.
- Keep new domain concepts out of window code-behind and out of ad-hoc helper files.

### 2.5 Structured logging `[~]`
- Replace `Debug.WriteLine` calls in production paths with a proper logging framework (Serilog or `Microsoft.Extensions.Logging`).
- Essential for diagnosing issues in release builds where Debug output is unavailable.
- Fix bare `catch` blocks in `OllamaProvider.Chat.cs` and `LocalApiService.cs` to log at minimum debug level.
- Recent progress: the highest-signal production paths now route warnings through `AppLogger` instead of `Debug.WriteLine`.

### 2.6 Fix resource and event leaks `[~]`
- Ensure `HttpResponseMessage` is disposed on all exception paths in streaming providers.
- Unsubscribe event handlers in `ChatService` when the orchestrator is replaced or the service is disposed.
- Audit remaining `IDisposable` implementations for completeness.
- Recent progress: Ollama streaming responses are disposed on all paths, `ChatService` unsubscribes on dispose, and the local API listener now logs unexpected failures instead of swallowing them.

## 3. Quality Bar

### 3.1 Coverage on meaningful paths `[ ]`
- Raise coverage on the workflows that matter most, not on trivial getters.
- Prioritize provider validation, local API, tool approval, onboarding, and persistence.

### 3.2 Release readiness `[ ]`
- Keep release builds clean.
- Keep screenshot assets current for every supported locale.
- Keep public-repo hygiene and contributor docs current.

### 3.3 Human-readable errors `[ ]`
- Prefer errors that explain what went wrong and what the user should do next.
- Avoid leaking unnecessary internals when the message can stay short and useful.

## 4. User-Facing Features (Prioritized)

### 4.1 Keyboard-first UX `[ ]`
- Global hotkey to summon the chat window from any app.
- Ctrl+K command palette for switching providers, models, and conversations mid-flow.
- Keyboard shortcuts for tool approval/denial, message navigation, and conversation actions.
- Power users shouldn't need the mouse for common workflows.

### 4.2 Conversation export and sharing `[ ]`
- Export conversations as Markdown, HTML, or PDF.
- Import conversations from exported files.
- Self-contained format that preserves messages, tool calls, and images.

### 4.3 Cost tracking dashboard `[ ]`
- Surface per-provider and per-conversation token usage and estimated cost.
- Token tracking data already exists for some providers; this is primarily a UI task.
- Show cumulative spend over time with basic filtering.

### 4.4 Conversation branching / forking `[ ]`
- Let users branch off from any message to explore alternative threads.
- Preserve the original conversation while creating a new branch.
- Tree-style navigation between branches.

### 4.5 Prompt templates / snippets `[ ]`
- User-defined prompt templates accessible from the composer.
- Different from assistant modes: quick-fire prefixes like "Explain this code" or "Review this diff."
- Support for parameterized templates with placeholder substitution.

### 4.6 Multi-model comparison `[ ]`
- Send the same prompt to 2-3 models side by side.
- Compare responses in a split view.
- Useful for evaluating which provider/model to use for a given task.

### 4.7 Provider-returned images `[ ]`
- Support provider image output in the transcript if the provider can actually return it.

### 4.8 Better context controls `[ ]`
- Let users control context size, caching, and summarization more explicitly.

### 4.9 Cleaner tool categories `[ ]`
- Replace the current coarse tool toggles with category-based controls.

### 4.10 Better onboarding defaults `[ ]`
- Keep first-run choices simple: one obvious cloud path, one obvious local path, and one obvious power-user path.

## 5. AI-Facing Features (Prioritized)

### 5.1 Agent mode `[ ]`
- A mode where the AI can autonomously chain tool calls without per-call approval.
- Configurable scope (which tool categories) and token budget.
- Auto-accept profiles are the foundation; this extends them into a full agent loop.
- Session-level budget tracking with automatic stop when limit is reached.

### 5.2 Structured context injection `[ ]`
- Let AIs declare what context they need (files, URLs, clipboard, recent messages) via a schema.
- Aire auto-attaches the requested context before sending the prompt.
- Reduces manual context assembly and enables richer AI workflows.

### 5.3 Tool result caching `[ ]`
- Cache tool results (file reads, web fetches) within a session.
- Repeated tool calls with identical parameters return cached results.
- Reduces latency and cost for iterative AI work.
- Cache invalidation on file modification or explicit flush.

### 5.4 Persistent AI memory per conversation `[ ]`
- Let the AI store and retrieve structured key-value notes within a conversation.
- Notes survive across sessions and are scoped to the conversation.
- Different from conversation history: this is structured recall for facts and decisions.

### 5.5 Workflow chains `[ ]`
- Define multi-step workflows where the output of one AI call feeds into the next.
- Support cross-provider chains (e.g., vision model analyzes image, then language model writes code).
- Template-based workflow definitions with variable passing between steps.

### 5.6 Local RAG integration `[ ]`
- Index local files (code, docs, notes) and expose them as a searchable tool.
- Ollama + local embeddings for fully offline operation.
- Configurable index scope (directories, file types, update frequency).

### 5.7 Better automation ergonomics `[ ]`
- Make provider selection, tool approval, and local API usage easier to script and test.

### 5.8 Plugin system `[ ]`
- Formalize MCP + tool categories into a plugin architecture.
- Let users and AIs install community tool packs without modifying the app.
- Plugin manifest format, discovery, and lifecycle management.

## 6. Operating Rules

1. Do not split architecture and behavior rewrites into one large pass.
2. Keep each change buildable and testable on its own.
3. If a change affects user-visible behavior, update the docs and screenshots in the same branch.
4. Prefer removing old plan files once their content has been merged into this roadmap.
5. If a task becomes obsolete, delete it instead of letting it linger as a stale markdown note.

## 7. Analysis Reports

- [Project analysis by Opus 4.6 (2026-04-09)](./project-analysis-opus-4.6-2026-04-09.md)
