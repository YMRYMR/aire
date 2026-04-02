# Aire Pre-GitHub Quality Execution Plan

This document is the shared execution plan for finishing Aire before creating the
public GitHub repository. It is written so multiple developers or models
(Codex, Claude, GLM-5 via zcode) can collaborate without duplicating work or
lowering the quality bar.

Status legend: `[ ]` not started · `[~]` in progress · `[x]` done

---

## 0. Non-negotiable quality rules

1. Do not add coverage-padding tests.
   Tests must validate real behavior, real regressions, or real security rules.

2. Do not fix a test by weakening product behavior unless the test expectation is
   genuinely wrong.

3. Keep architectural direction consistent:
   `UI -> Application -> Domain/Infrastructure`.

4. Prefer extending existing application services and domain contracts over
   reintroducing workflow logic into window code-behind.

5. Every completed item must end with:
   - clean build
   - targeted tests
   - full test suite when the change is broad or risky
   - updated docs if user-facing behavior changed

6. If a task reveals a product bug outside its immediate scope, either fix it in
   the same branch if tightly related, or document it here before moving on.

---

## 1. Final goals

### 1.1 z.ai provider works reliably  `[ ]`
Definition of done:
- provider validates successfully against the real z.ai API contract
- model loading works
- chat works
- tool-calling behavior is correct for supported models
- onboarding and settings behave correctly
- provider-specific tests exist and are truthful

### 1.2 Codex provider works reliably  `[ ]`
Definition of done:
- provider can validate when Codex CLI is installed and authenticated
- onboarding and settings expose Codex clearly
- in-app CLI installation is stable and informative
- user-facing error messages distinguish:
  - no CLI installed
  - CLI installed but not authenticated
  - CLI execution failed
- provider-specific tests cover path resolution, install flow, and runtime failure modes

### 1.3 Main-window tools control supports category selection  `[ ]`
Definition of done:
- the current tools toggle is replaced or extended with a category selection UI
- user can enable/disable categories such as `filesystem`, `browser`, `agent`,
  `mouse`, `keyboard`, `system`, `email`
- provider runtime only sees the enabled categories
- selection persists across app restarts
- settings/help explain the behavior in plain language
- tests cover persistence and runtime enforcement

### 1.4 Architecture is homogeneous across the whole app  `[ ]`
Definition of done:
- no remaining meaningful workflow logic is hidden in window code-behind
- stable request/result/value types live in domain contracts where appropriate
- application services depend on ports/interfaces where infrastructure isolation matters
- the remaining UI code is genuinely UI-centric: rendering, dialogs, visibility,
  focus, progress, layout
- docs match the actual architecture

### 1.5 Coverage reaches at least 80%  `[ ]`
Definition of done:
- whole-app line coverage >= 80%
- tests remain truthful and behavior-oriented
- high-risk seams have strong branch coverage, especially:
  - providers
  - local API
  - tool approval/execution
  - onboarding/settings workflows
  - window state persistence

### 1.6 Main window reliably restores last position  `[ ]`
Definition of done:
- position restore works on multi-monitor setups
- off-screen windows are brought back safely
- maximized and normal states behave correctly
- tests cover persisted window state rules

### 1.7 Repo is truly ready for public release  `[ ]`
Definition of done:
- build is clean
- tests are green
- docs are current
- manual smoke checklist passes
- no known blocking provider or UI regressions remain

### 1.8 Main-window chat search is visible and reliable  `[ ]`
Definition of done:
- `Ctrl+F` reliably opens chat search in the main window
- search UI is discoverable without memorizing the shortcut
- next/previous navigation works
- focus lands in the search box when opened
- tests cover the wiring and user-visible behavior where practical

### 1.9 Streaming responses are supported cleanly  `[ ]`
Definition of done:
- supported providers can stream into the main chat UI
- partial assistant responses render progressively without corrupting transcript state
- stop/cancel behavior is reliable
- tool-call and follow-up semantics still work under streaming
- tests cover the application/service streaming paths

### 1.10 MCP discovery and install/uninstall UX is easy  `[ ]`
Definition of done:
- users can discover MCP servers from curated sources or public registries/repositories
- install and uninstall flows are understandable and safe
- MCP settings make clear what is local, what is third-party, and what is enabled
- tests cover catalog parsing, install/uninstall flow, and error handling

### 1.11 Providers can return images in chat responses  `[ ]`
Definition of done:
- providers that support image output can return images through a shared Aire response contract
- chat UI renders provider-returned images safely
- persistence/transcript behavior for image responses is defined clearly
- tests cover the non-UI image result pipeline

### 1.12 Nice to have: image generation in chat  `[ ]`
Definition of done:
- the user can request generated images directly from chat
- generation results appear inline in the conversation
- provider selection or capability gating is clear
- errors and cost/latency expectations are understandable

### 1.13 Nice to have: Aire orchestration mode for multi-AI tasks  `[ ]`
Definition of done:
- one AI can act as the primary planner while delegating bounded subtasks to subordinate AIs
- approval, persistence, and audit semantics remain under Aire control
- configuration is understandable and safe
- this does not collapse the architecture into a god orchestrator

### 1.14 Context and cache handling reduces token waste  `[ ]`
Definition of done:
- shared context management trims or summarizes history intentionally
- reusable cache layers exist where they actually save cost or latency
- provider adapters can consume cached/shared context without breaking Aire semantics
- tests cover cache invalidation and context-shaping rules

### 1.15 Nice to have: auto-accept profiles are editable and have good defaults  `[ ]`
Definition of done:
- settings allow adding, deleting, and selecting auto-accept profiles/configurations
- sensible built-in profiles exist, such as `Developer` and `News browser`
- profile behavior is understandable in plain language
- tests cover persistence and profile selection logic

### 1.16 Main window supports an explicit AI mode selector  `[ ]`
Definition of done:
- main window exposes a mode selector such as `Developer`, `Creative writer`, `Architect`, `Teacher`
- selected mode is passed to providers through a shared semantic contract, not ad-hoc prompt hacks
- mode selection persists and is easy to change
- tests cover persistence and request-shaping behavior

---

## 2. Suggested execution order

This order is intentional. It minimizes rework and keeps the highest-risk paths
stabilized first.

1. Fix provider reliability:
   - z.ai
   - Codex
2. Fix main window position restore
3. Implement tools category selection
4. Finish architecture homogenization
5. Push truthful coverage toward 80%
6. Run final manual smoke pass
7. Create the public repo

Additional product track after the core blockers:
8. restore visible/reliable search in the main chat
9. streaming responses
10. context/cache improvements
11. MCP discovery/install UX
12. provider-returned images
13. mode selector / auto-accept profiles
14. optional image generation
15. optional multi-AI orchestration

Reasoning:
- provider failures are user-visible and undermine trust immediately
- window-position bugs are frustrating and cheap to regression-test
- category-based tools control affects both UX and runtime behavior
- architecture cleanup should happen before the large final coverage push
- coverage should target the final architecture, not the pre-refactor shape
- search and streaming are core chat usability features and should come before
  larger nice-to-have platform work
- context/cache should precede any ambitious orchestration work, otherwise the
  token/cost story will be poor
- MCP discovery, image output, and mode/profile features are valuable but less
  urgent than core chat reliability

---

## 3. Known code touchpoints

These are the current likely starting points, based on the repo structure as of
April 1, 2026.

### 3.1 z.ai provider

Primary files:
- `Aire.Core/Providers/ZaiProvider.cs`
- `Aire.Core/Providers/OpenAiProvider.cs`
- `Aire/Services/Providers/ProviderConfigurationWorkflowService.cs`
- `Aire/UI/OnboardingWindow.xaml`
- `Aire/UI/OnboardingWindow.xaml.cs`

Likely work:
- validate z.ai base URL and SDK configuration
- confirm chat and models endpoints
- verify compatibility with the current OpenAI SDK assumptions
- add provider tests and real smoke coverage where feasible

### 3.2 Codex provider

Primary files:
- `Aire.Core/Providers/CodexProvider.cs`
- `Aire/Application/Abstractions/ICodexManagementClient.cs`
- `Aire/Application/Providers/CodexActionApplicationService.cs`
- `Aire/Services/Providers/CodexManagementClient.cs`
- `Aire/UI/OnboardingWindow/`
- `Aire/UI/SettingsWindow/Providers/`
- `Aire.Tests/Providers/CodexProviderTests.cs`

Likely work:
- distinguish install/auth/runtime failures cleanly
- validate CLI invocation shape
- improve smoke-test behavior and user guidance
- verify onboarding/settings UX end to end

### 3.3 Tools category selection

Primary files:
- `Aire/UI/MainWindow/Chat/MainWindow.ToolsToggle.cs`
- `Aire/UI/MainWindow.xaml`
- `Aire/Core/Providers/IAiProvider.cs`
- `Aire.Core/Providers/SharedToolDefinitions*.cs`
- `Aire/Application/Tools/`
- `Aire/Services/Policies/`

Likely work:
- create a domain or application contract for enabled tool categories
- persist the selection
- update provider/tool-definition filtering to honor it
- add a compact but clear selector UI

### 3.4 Main window position restore

Primary files:
- `Aire/UI/MainWindow/Shell/MainWindow.WindowState.cs`
- `Aire/App.xaml.cs`
- `Aire/Services/TrayIconService.cs`
- `Aire.Tests/UI/UiWorkflowRegressionTests.cs`

Likely issue:
- current restore logic uses `SystemParameters.WorkArea`, which is usually not
  sufficient for multi-monitor window restoration

Likely fix direction:
- use screen-aware restore rules
- detect off-screen persisted positions
- preserve maximized/normal semantics safely

### 3.5 Architecture homogenization

Primary docs:
- `docs/architecture/overview.md`
- `docs/architecture/developer-map.md`

Likely remaining hotspots:
- `Aire/UI/MainWindow/`
- `Aire/UI/SettingsWindow/Providers/`
- `Aire/UI/OnboardingWindow/`

Goal:
- remove remaining application/orchestration logic from UI files where it is
  still meaningful to do so

### 3.6 Coverage to 80%

Primary test files:
- `Aire.Tests/ServiceWorkflowRegressionTests.cs`
- `Aire.Tests/UI/UiWorkflowRegressionTests.cs`
- `Aire.Tests/HigherYieldCoverageTests.cs`

Current rule:
- prefer direct application/service tests
- only use UI-adjacent tests where actual wiring matters

### 3.7 Chat search and streaming

Primary files:
- `Aire/UI/MainWindow/Search/`
- `Aire/UI/MainWindow.xaml`
- `Aire/UI/Controls/`
- `Aire/Services/ChatService.cs`
- `Aire/Services/Workflows/ChatTurnWorkflowService.cs`

Likely work:
- restore `Ctrl+F` and search-panel discoverability
- verify routed key handling and focus behavior
- move streaming-safe transcript updates through application/service seams

### 3.8 MCP discovery/install UX

Primary files:
- `Aire/UI/SettingsWindow/Controls/McpConnectionsPaneControl.xaml`
- `Aire/UI/SettingsWindow/Connections/`
- `Aire/Services/Mcp/`
- `Aire/Application/`

Likely work:
- define curated MCP catalog format
- add install/uninstall application services
- keep third-party trust boundaries explicit

### 3.9 Images, modes, and profiles

Primary files:
- `Aire/Core/Providers/`
- `Aire/Application/Providers/`
- `Aire/UI/MainWindow/`
- `Aire/UI/SettingsWindow/`
- `Aire/Services/Policies/`

Likely work:
- shared image-result contracts
- mode/profile domain contracts
- UI controls that remain thin and app-layer driven

---

## 4. Work breakdown by phase

### Phase A. Provider reliability  `[ ]`

#### A1. z.ai diagnosis and fix  `[ ]`
Tasks:
- verify real z.ai request/response shape
- confirm model list URL
- confirm chat URL
- confirm whether tool-calling is supported in the same OpenAI-compatible shape
- fix initialization or request-building logic as needed
- add truthful provider tests

Acceptance evidence:
- targeted z.ai tests
- manual validation notes against the real API if available
- no regression in OpenAI-compatible providers

#### A2. Codex diagnosis and fix  `[ ]`
Tasks:
- verify CLI execution on a machine with working CLI
- improve error differentiation:
  - missing CLI
  - inaccessible Store package only
  - unauthenticated CLI
  - runtime timeout/failure
- verify in-app install flow
- improve onboarding/settings copy
- add truthful tests for these cases

Acceptance evidence:
- targeted Codex tests
- validation of install path
- validation of runtime path

### Phase B. Main window state reliability  `[ ]`

#### B1. Position restore on multi-monitor systems  `[ ]`
Tasks:
- reproduce the issue
- replace single-work-area assumptions with screen-aware restore logic
- handle disconnected monitor scenarios
- handle maximized restore correctly
- add state persistence tests

Acceptance evidence:
- tests for valid, off-screen, and disconnected-monitor-like coordinates
- manual smoke notes on multi-monitor behavior

### Phase C. Tools category control  `[ ]`

#### C1. Domain/application model for enabled categories  `[ ]`
Tasks:
- define the persisted shape for category selection
- ensure tool definition filtering uses it
- keep backward compatibility with the current single enabled/disabled state if needed

#### C2. Main window UX for category selection  `[ ]`
Tasks:
- replace the current binary toggle with a category-aware control
- keep the UX simple for non-technical users
- expose clear category names and descriptions

Acceptance evidence:
- tests proving category filtering is enforced
- persistence across restarts
- help/documentation update

### Phase D. Architecture homogenization  `[ ]`

#### D1. Remaining UI orchestration audit  `[ ]`
Tasks:
- audit UI files for remaining application/domain logic
- only extract code that represents real workflow, not mere rendering glue
- keep the architecture consistent with the current application layer

#### D2. Domain contract cleanup  `[ ]`
Tasks:
- move any remaining stable workflow/result/value shapes into domain contracts
- avoid nested service-owned records for shared concepts

Acceptance evidence:
- architecture docs updated
- no meaningful workflow logic left in window code-behind

### Phase E. Truthful coverage push to 80%  `[ ]`

#### E1. Measure current baseline  `[ ]`
Tasks:
- run fresh full coverage
- identify the lowest-coverage high-value seams

#### E2. Incremental coverage campaigns  `[ ]`
Priority order:
1. providers
2. tool approval/execution
3. local API
4. onboarding/settings workflows
5. window state persistence

Rules:
- no fake tests
- prefer direct application/service tests
- when a UI test is required, make it check real user-visible behavior

Acceptance evidence:
- full coverage report >= 80% line coverage
- branch coverage materially improved as well

### Phase F. Public-release readiness  `[ ]`

#### F1. Final docs and smoke pass  `[ ]`
Tasks:
- recheck README, CONTRIBUTING, SECURITY
- run the manual smoke checklist
- record any remaining non-blocking follow-ups separately

#### F2. GitHub repo creation  `[ ]`
Tasks:
- final git hygiene check
- final first-public-history review
- create/push repo only when all blocking items are done

### Phase G. Core chat usability and product expansion  `[ ]`

#### G1. Main-window search recovery  `[ ]`
Tasks:
- restore `Ctrl+F` behavior
- make search discoverable in the UI
- verify next/previous navigation and focus

#### G2. Streaming responses  `[ ]`
Tasks:
- define the streaming update contract through the current architecture
- stream partial assistant text safely into the transcript
- verify cancellation, tool calls, and follow-up handling

#### G3. Context and cache strategy  `[ ]`
Tasks:
- define what can be cached safely
- add context-shaping rules that save tokens without hiding needed state
- document invalidation and provider-adapter usage

#### G4. MCP discovery/install UX  `[ ]`
Tasks:
- define curated/public MCP source strategy
- add install/uninstall application flows
- make discovery safe and understandable

#### G5. Modes, profiles, and images  `[ ]`
Tasks:
- add AI mode selector with shared semantics
- add editable auto-accept profiles with defaults
- design provider-returned image contract
- only then consider image generation as a follow-up

#### G6. Optional advanced orchestration  `[ ]`
Tasks:
- only start after streaming, context/cache, and mode semantics are stable
- keep one coordinator contract, not a god orchestrator
- require strong audit and approval semantics

---

## 5. Recommended parallel collaboration model

This section is specifically for multi-model collaboration.

### Codex
Best suited for:
- cross-cutting implementation
- architectural cleanup
- integrating changes across multiple files
- test fixing and build/test validation

Suggested ownership:
- architecture homogenization
- tools category selection
- final integration and test validation

### Claude
Best suited for:
- provider debugging with careful reasoning
- UX/help copy improvements
- documentation refinement
- investigating subtle behavioral bugs

Suggested ownership:
- z.ai diagnosis and fix
- Codex provider UX/error-message refinement
- release-doc polish

### GLM-5 / zcode
Best suited for:
- large test-writing batches
- coverage expansion campaigns
- exhaustive branch/edge-case exploration
- systematic refactor follow-through

Suggested ownership:
- coverage campaigns
- window-state regression tests
- provider regression test expansion

### Important note about orchestration

Codex cannot assume direct control of the zcode desktop app just because both
apps are running. Coordination should happen through:
- this shared markdown plan
- clear file ownership slices
- explicit handoff notes and acceptance criteria
- repo-based integration and validation

### Merge rule

Do not let multiple contributors edit the same write scope in parallel unless the
changes are intentionally coordinated.

Recommended parallel slices:
- Slice 1: provider reliability
- Slice 2: main window state
- Slice 3: tools category model and tests
- Slice 4: coverage-only campaign after the architecture stabilizes

### Suggested first assignment split

If all three collaborators are available, use this split:

- Codex:
  - own the architecture-sensitive work
  - implement the tools-category model and UI
  - integrate final cross-cutting changes

- Claude:
  - own `z.ai` diagnosis/fix
  - review `Codex` provider UX and failure handling
  - improve release-facing wording when needed

- GLM-5 / zcode:
  - own the big truthful test campaigns
  - expand provider tests
  - expand window-state tests
  - push coverage after architecture stabilizes

---

## 6. Required evidence per completed item

Every completed item should leave behind:

1. files changed
2. why the bug existed
3. what changed structurally
4. what tests were added or updated
5. build result
6. targeted/full test result
7. any remaining caveat

This should be recorded either:
- in the PR/commit notes, or
- as a short update appended to this plan

---

## 7. Stop conditions

Pause and reassess before continuing if any of these happen:

- tests start becoming coverage-oriented instead of behavior-oriented
- architecture cleanup starts creating pass-through services with no real ownership
- provider fixes require undocumented hacks that would not be stable in public
- the tools-category feature becomes confusing or unsafe for normal users
- coverage progress stalls because the remaining paths are mostly UI-only noise

If that happens, document the issue and revise the plan instead of forcing it.

---

## 8. Suggested first concrete moves

1. Reproduce and fix z.ai.
2. Reproduce and fix Codex runtime/auth failures after CLI install.
3. Fix main window multi-monitor restore.
4. Design the persisted model for tool categories.
5. Only then start the big push toward 80% coverage.

That sequence should give the best balance of user value, regression safety, and
architectural cleanliness.
