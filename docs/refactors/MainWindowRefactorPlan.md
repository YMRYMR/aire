# MainWindow Refactor Plan

## Goals

- Reduce the size of [MainWindow.xaml]Aire/MainWindow.xaml) and [MainWindow.xaml.cs]Aire/MainWindow.xaml.cs) without changing behavior.
- Make `MainWindow` a shell that owns only window-level UI concerns.
- Move feature logic into smaller, testable files organized by responsibility.
- Create stable seams so future changes do not keep expanding the main window files.

## Constraints

- Preserve all current behavior during each step.
- Keep each extraction buildable and testable on its own.
- Prefer low-risk moves first: self-contained partials, facades, resource dictionaries, then controls.
- Do not mix large visual extraction with large orchestration extraction in the same step.

## Target Folder Layout

- `Aire/UI/MainWindow/Api/`
- `Aire/UI/MainWindow/Shell/`
- `Aire/UI/MainWindow/Conversations/`
- `Aire/UI/MainWindow/Providers/`
- `Aire/UI/MainWindow/Chat/`
- `Aire/UI/MainWindow/Speech/`
- `Aire/UI/MainWindow/Search/`
- `Aire/UI/MainWindow/Attachments/`
- `Aire/UI/MainWindow/Controls/`
- `Aire/UI/MainWindow/Resources/`

## End-State Shape

### MainWindow.xaml

Should contain only shell layout and control composition:

- title/header host
- sidebar host
- chat transcript host
- composer host
- shell-level splitters/overlays

### MainWindow.xaml.cs

Should contain only:

- constructor and `InitializeComponent`
- shell lifecycle
- focus/visibility/window movement
- cross-control wiring
- shell-only event forwarding

It should not directly own:

- database orchestration
- provider orchestration
- local API orchestration
- chat/AI execution flow
- tool-approval orchestration
- speech orchestration
- conversation CRUD/search logic

## Extraction Order

### Phase 1: Safe Structural Shrink

1. Move local API methods into `UI/MainWindow/Api/MainWindow.Api.cs`
2. Move window state persistence into `UI/MainWindow/Shell/MainWindow.WindowState.cs`
3. Keep these as `partial MainWindow` first to preserve behavior with minimal risk

### Phase 2: Shell-Only Presenter / State Service

1. Introduce `MainWindowStateService`
2. Introduce `MainWindowShellPresenter`
3. Move:
   - tray/pin/topmost behavior
   - settings/help/browser window opening
   - restore/reset window state

### Phase 3: Sidebar UI Extraction

1. Extract the conversation sidebar into:
   - `UI/MainWindow/Controls/ConversationSidebarControl.xaml`
   - `UI/MainWindow/Controls/ConversationSidebarControl.xaml.cs`
   - `UI/MainWindow/Resources/ConversationSidebarTemplates.xaml`
2. Keep behavior in `MainWindow` initially, expose control events/DPs only

Move first:

- sidebar search row
- new conversation button
- conversation list template/style
- rename/delete UI interaction surface

### Phase 4: Feature Splits

- `Conversations/`
- `Providers/`
- `Chat/`
- `Speech/`
- `Search/`
- `Attachments/`

Each phase starts as partial extraction first, then graduates into presenters/controllers/services.

## Safety Rules

- one feature slice at a time
- no behavior rewrite during extraction
- build after every extraction
- run focused tests after every extraction
- only convert partial slices into presenters/controllers after the seam is stable

