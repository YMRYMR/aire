# App Refactor Continuation Plan

## Current State

- `MainWindow` root files are small and most behavior is already split across `UI/MainWindow/*`.
- `SettingsWindow` is partially refactored:
  - provider list pane is extracted into a control
  - provider logic is split across focused partial files
  - helper/view-model classes moved out of the root window file
  - startup/loading logic moved out of `SettingsWindow.xaml.cs`
- `OnboardingWindow`, `WebViewWindow`, `HelpWindow`, and other secondary windows are already reduced.

## Remaining SettingsWindow Work

### 1. Connections Pane Extraction

Safest order:

1. Extract `Local API access` block into a small forwarding control.
2. Extract `Email Accounts` block into a forwarding control.
3. Extract `MCP Servers` block into a forwarding control.

Rules:

- keep all current behavior in `SettingsWindow` partial files at first
- forward events from controls back to the existing handlers
- expose only the minimum required named elements through accessors
- validate app launch after each extraction

### 2. Appearance / Voice / Auto-accept Tabs

These are still large XAML sections but are relatively self-contained.

Recommended order:

1. `Voice`
2. `Appearance`
3. `Auto-accept`

Reason:

- `Voice` has the narrowest dependency surface
- `Appearance` is broader but still mostly local UI state
- `Auto-accept` has many named controls, so it is best left until the forwarding-control pattern is fully stable

### 3. Provider Edit Form Extraction

Leave this for last inside `SettingsWindow`.

Target split:

- `ProviderListPaneControl` already extracted
- next possible controls:
  - `ProviderEditFormControl`
  - `ProviderCapabilityTestsControl`

This is higher risk because the form is tightly coupled to model loading, autosave, and provider metadata.

## Remaining MainWindow Work

The main remaining work is architectural, not line-count-driven.

Priority:

1. extract tool execution branch inside the AI loop into a dedicated coordinator or service
2. extract chat turn orchestration into a session/chat coordinator
3. keep `MainWindow` partials focused on UI state, not orchestration rules

Suggested target classes:

- `MainChatCoordinator`
- `MainToolExecutionCoordinator`
- `MainConversationCoordinator`

## Remaining App-Wide Work

### Window / UI Surfaces

Review remaining large XAML/code-behind surfaces and continue the same pattern:

1. `SettingsWindow` remaining panes
2. `ImageViewerWindow`
3. any still-large shared controls or resource-heavy windows

### Services / Data Layers

Large files that still mix concerns and should be split by responsibility:

1. `Data/DatabaseService.cs`
2. `Services/OllamaService.cs`
3. `Services/SpeechRecognitionService.cs`
4. `UI/LinkedText.cs`

Recommended split strategy:

- move helper/domain types to `Models` or `Contracts`
- split service files by feature slices, not utility methods
- keep public API unchanged while moving internals

## Safety Rules

- avoid simultaneous XAML extraction and startup-path changes in the same pass
- for WPF controls, prefer forwarding controls plus accessors before deeper MVVM conversion
- after each extraction:
  - `dotnet build .\aire.sln -m:1`
  - `dotnet test .\Aire.Tests\Aire.Tests.csproj --no-build`
- if a change touches startup, window resources, or control templates, do an app launch smoke test before proceeding

## Next Recommended Step

Extract `SettingsWindow` connections pane in three stages:

1. `LocalApiAccessPaneControl`
2. `EmailConnectionsPaneControl`
3. `McpConnectionsPaneControl`

This keeps the next refactor incremental and minimizes the chance of another launch regression.
