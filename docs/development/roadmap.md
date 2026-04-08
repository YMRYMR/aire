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

### 2.2 Strengthen application-service tests `[ ]`
- Add direct tests for important application services instead of relying only on WPF-backed tests.
- Focus first on chat orchestration, provider workflows, and settings persistence.

### 2.3 Reduce test fragility `[ ]`
- Remove remaining assumptions that depend on `GetUninitializedObject` or hidden WPF state.
- Prefer test seams that construct the real object graph when feasible.

### 2.4 Keep layering honest `[ ]`
- Preserve the `UI -> Application -> Domain/Infrastructure` direction.
- Keep new domain concepts out of window code-behind and out of ad-hoc helper files.

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

## 4. Nice Ideas Worth Considering

### 4.1 Provider-returned images `[ ]`
- Support provider image output in the transcript if the provider can actually return it.

### 4.2 Better context controls `[ ]`
- Let users control context size, caching, and summarization more explicitly.

### 4.3 Cleaner tool categories `[ ]`
- Replace the current coarse tool toggles with category-based controls.

### 4.4 Better automation ergonomics `[ ]`
- Make provider selection, tool approval, and local API usage easier to script and test.

### 4.5 Better onboarding defaults `[ ]`
- Keep first-run choices simple: one obvious cloud path, one obvious local path, and one obvious power-user path.

## 5. Operating Rules

1. Do not split architecture and behavior rewrites into one large pass.
2. Keep each change buildable and testable on its own.
3. If a change affects user-visible behavior, update the docs and screenshots in the same branch.
4. Prefer removing old plan files once their content has been merged into this roadmap.
5. If a task becomes obsolete, delete it instead of letting it linger as a stale markdown note.
