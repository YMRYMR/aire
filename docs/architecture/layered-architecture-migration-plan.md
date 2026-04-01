# Layered Architecture Migration Plan

## Goal

Move Aire from a mostly feature-organized desktop app into a clearer layered architecture with explicit boundaries between:

- `UI`
- `Application`
- `Domain`
- `Infrastructure`

The goal is not to do a theoretical rewrite. The goal is to make the code easier to understand, easier to test, and safer for outside contributors to extend without breaking unrelated areas.

## Current State

Today the repository is much cleaner than it was originally, but the layers are still blurred:

- [Aire]Aire) contains WPF UI, workflow services, persistence, provider implementations, and Windows-specific integrations.
- [Aire.Core]Aire.Core) contains shared provider contracts, parsing, tool logic, and some infrastructure-like services.
- Workflow logic has already started moving out of windows into services, but some important runtime behavior still lives in UI-adjacent coordinators.
- Persistence and provider runtime code are still accessed fairly directly from app code.

This means the project is `organized`, but not yet `layered`.

## Target Architecture

### 1. UI layer

Owns:

- WPF windows, controls, XAML
- converters
- view-only state
- event forwarding
- rendering of results returned by application services

Must not own:

- business rules
- provider-selection rules
- tool approval policy rules
- persistence decisions
- request orchestration logic

Candidate project:

- `Aire.UI.Wpf`

Short-term mapping:

- keep the current [Aire/UI]Aire/UI) folder, but reduce it to shells, controls, and adapters over time

### 2. Application layer

Owns:

- workflows
- use cases
- orchestration
- conversation lifecycle
- provider activation
- onboarding flow
- tool approval flow
- chat turn progression

Must not own:

- WPF controls or dispatcher concerns
- direct SQLite access
- direct WebView2 access
- DPAPI details
- provider-specific HTTP internals

Candidate project:

- `Aire.Application`

Short-term mapping:

- many files already point in this direction:
  - [ChatTurnWorkflowService.cs]Aire/Services/Workflows/ChatTurnWorkflowService.cs)
  - [ToolExecutionWorkflowService.cs]Aire/Services/Workflows/ToolExecutionWorkflowService.cs)
  - [ProviderActivationWorkflowService.cs]Aire/Services/Workflows/ProviderActivationWorkflowService.cs)
  - [ProviderConfigurationWorkflowService.cs]Aire/Services/Providers/ProviderConfigurationWorkflowService.cs)

### 3. Domain layer

Owns:

- core models
- capability concepts
- tool metadata
- approval rules
- provider-independent business concepts
- value objects and enums

Must not own:

- WPF
- SQLite
- HTTP clients
- WebView2
- environment-variable lookups
- machine-specific APIs

Candidate project:

- `Aire.Domain`

Short-term mapping:

- move or consolidate provider-independent models and rules here over time:
  - provider capability concepts
  - tool-definition abstractions
  - conversation/message abstractions
  - policy and classification rules

### 4. Infrastructure layer

Owns:

- SQLite
- DPAPI
- HTTP integrations
- Ollama host/runtime probing
- email transport
- WebView2/browser bridge
- filesystem and system tool implementations
- MCP client/process management

Candidate project:

- `Aire.Infrastructure`

Short-term mapping:

- most of the current [Aire/Data]Aire/Data), [Aire/Services]Aire/Services), and provider runtime code is infrastructure

## Dependency Rules

The desired dependency direction is:

- `UI -> Application`
- `Application -> Domain`
- `Infrastructure -> Domain`
- `Infrastructure -> Application` only to implement application-defined interfaces
- `UI` must not reference `Infrastructure` directly except during temporary migration
- `Domain` must reference nothing from `UI` or `Infrastructure`

That means:

- repositories and gateways are defined by `Application` or `Domain`
- concrete database/browser/provider/email implementations live in `Infrastructure`
- `UI` consumes interfaces and use-case services, not storage classes directly

## Proposed Project Shape

Target solution shape:

- `Aire.UI.Wpf`
- `Aire.Application`
- `Aire.Domain`
- `Aire.Infrastructure`
- `Aire.Tests`

Transition shape before the final split:

- keep `Aire` and `Aire.Core`
- add folder and namespace boundaries that mirror the future projects
- move code gradually, then split physical projects once dependencies are clean

## Migration Principles

1. Do not rewrite working features just to satisfy theory.
2. Move one workflow seam at a time.
3. Introduce interfaces before moving concrete implementations.
4. Keep builds and tests green after every slice.
5. Add direct service tests when logic leaves the UI.
6. Prefer compatibility adapters over massive one-shot rewrites.

## What To Move First

### Phase 1: Introduce ports around persistence

Create repository/service interfaces for the highest-traffic persistence dependencies:

- `IProviderRepository`
- `IConversationRepository`
- `ISettingsRepository`
- `IEmailAccountRepository`
- `IApiAccessStateStore` or fold into settings/app-state abstractions

Concrete implementation:

- wrap [DatabaseService.cs]Aire/Data/DatabaseService.cs) and its partials behind these interfaces

Why first:

- the UI and workflows still touch persistence too directly
- this is the cleanest way to separate `Application` from `Infrastructure`

### Phase 2: Create application services for the major workflows

Target services:

- `ChatSessionApplicationService`
- `ProviderActivationApplicationService`
- `ToolApprovalApplicationService`
- `OnboardingApplicationService`
- `SettingsProviderConfigurationApplicationService`

These services should own:

- decisions
- sequencing
- persistence calls through interfaces
- normalized results for the UI

The UI should become:

- collect input
- call app service
- render returned result

### Phase 3: Narrow the MainWindow coordinators into adapters

Most important target:

- [MainWindow.ChatCoordinator.cs]Aire/UI/MainWindow/Coordinators/MainWindow.ChatCoordinator.cs)
- [MainWindow.ToolApprovalCoordinator.cs]Aire/UI/MainWindow/Coordinators/MainWindow.ToolApprovalCoordinator.cs)
- [MainWindow.ProviderCoordinator.cs]Aire/UI/MainWindow/Coordinators/MainWindow.ProviderCoordinator.cs)

Desired end state:

- they should mostly translate WPF state into service calls and translate service results into UI messages
- they should not decide workflow branches directly

### Phase 4: Consolidate domain models

Focus on models currently spread across `Aire` and `Aire.Core`:

- provider contracts and metadata
- conversation/message abstractions
- tool metadata
- approval-policy concepts
- provider capability rules

This is where `Aire.Domain` should become real.

### Phase 5: Move infrastructure implementations behind interfaces

Best candidates:

- `DatabaseService`
- `LocalApiService`
- `SecureStorage`
- provider HTTP implementations
- `EmailService`
- MCP client/manager
- browser host integrations

### Phase 6: Split physical projects

Only do this after the dependencies already follow the intended direction.

At that point:

- move `Application` classes into `Aire.Application`
- move pure models/rules into `Aire.Domain`
- move concrete I/O implementations into `Aire.Infrastructure`
- keep WPF in `Aire.UI.Wpf`

## First Safe Implementation Slice

The first implementation slice should be intentionally small and high-value.

Recommended first slice:

1. define `IProviderRepository`, `IConversationRepository`, and `ISettingsRepository`
2. adapt [DatabaseService.cs]Aire/Data/DatabaseService.cs) to implement them
3. introduce `ChatSessionApplicationService`
4. make `MainWindow` coordinators call that application service instead of using database/provider services directly
5. add direct tests for the new application service

Why this slice:

- it improves both layering and testability
- it reduces direct UI-to-data coupling
- it does not require a full project split yet

## Candidate Interfaces

Examples of the kinds of ports to introduce:

- `IProviderRepository`
  - load providers
  - save provider order
  - insert/update/delete provider
- `IConversationRepository`
  - create conversation
  - load conversation
  - list conversations
  - save messages
  - delete conversations
- `ISettingsRepository`
  - read/write settings
- `IProviderRuntimeFactory`
  - create runtime providers from normalized configs
- `IToolExecutionGateway`
  - execute tools
  - describe tool requests
- `INotificationGateway`
  - tray/system notifications

The exact interface list should stay small. Only introduce a port when it represents a real boundary.

## Risks

### Risk: Too many abstractions too early

Mitigation:

- only add interfaces for real boundaries
- do not create pass-through wrappers for trivial helpers

### Risk: Temporary duplication during migration

Mitigation:

- use compatibility adapters
- delete the old path as soon as the new boundary is stable

### Risk: Project split before dependencies are ready

Mitigation:

- clean namespaces and dependencies first
- physical project split comes later

### Risk: Test instability from WPF-heavy flows

Mitigation:

- move logic into application services first
- test those services directly
- keep UI tests thin and selective

## Success Criteria

The migration is working when:

- windows mainly render and forward events
- workflows live in application services
- repositories/gateways isolate storage and integrations
- domain models and rules no longer depend on WPF or SQLite
- contributors can implement features by touching one layer intentionally instead of crossing several
- tests mostly target application and domain services rather than private UI behavior

## Recommended Next Step

Start with the repository boundary slice:

1. add repository interfaces
2. adapt `DatabaseService`
3. introduce `ChatSessionApplicationService`
4. migrate one `MainWindow` path to it
5. test the new service directly

That is the smallest change that creates a real layered-architecture seam instead of just another organizational refactor.
