# Architecture Overview

## High-Level Layout

- `Aire/`: WPF desktop application and Windows-specific integrations
- `Aire.Core/`: provider abstractions, shared services, parsing, and tool logic
- `Aire.Tests/`: automated regression and workflow tests
- `docs/`: refactor notes and contributor-facing documentation

## App Layers

### UI shell

The WPF windows live under `Aire/UI/`.

Key windows:
- `Aire/UI/MainWindow.xaml`
- `Aire/UI/SettingsWindow.xaml`
- `Aire/UI/OnboardingWindow.xaml`
- `Aire/UI/HelpWindow.xaml`
- `Aire/UI/WebViewWindow.xaml`

Windows are split into feature folders so behavior is easier to find:
- `Shell/`: lifecycle, sizing, startup, window concerns
- `Controls/`: reusable WPF controls
- `Providers/`, `Connections/`, `Voice/`, `Search/`, etc.: feature-specific logic

UI code should mostly:
- gather user input
- call application services
- render returned state
- perform UI-only effects such as focus, progress bars, dialogs, and visibility changes

### Application layer

Application workflows live under `Aire/Application/`.

Important areas:
- `Chat/`: chat session, transcript, and turn workflows
- `Providers/`: provider setup, activation, catalog, editor, and Ollama workflows
- `Tools/`: approval and execution workflows
- `Api/`: local API request/result shaping
- `Mcp/`: MCP server startup and configuration management
- `Connections/`: email account configuration management
- `Settings/`: general application settings read/write
- `Abstractions/`: application ports into infrastructure

### Domain layer

Stable business contracts live under `Aire/Domain/`.

Current examples:
- `Aire/Domain/Providers/ProviderContracts.cs`
- `Aire/Domain/Providers/OllamaContracts.cs`

These are the request/result/value shapes that should remain stable regardless of UI or infrastructure details.

### Infrastructure

Concrete platform and I/O implementations live under `Aire/Services/` and `Aire/Data/`,
plus parts of `Aire.Core/`.

Examples:
- `Aire/Services/AppearanceService.cs`
- `Aire/Services/LocalApiService.cs`
- `Aire/Services/ToolExecutionService.cs`
- `Aire/Services/Providers/OllamaManagementClient.cs`
- `Aire.Core/Services/CapabilityTestService.cs`

Persistent local window/app state is stored under `%LOCALAPPDATA%\Aire`.

## Provider Model

Provider abstractions live in `Aire.Core/Providers/`.

Important pieces:
- shared provider contracts and request/response models
- provider-specific implementations such as OpenAI and Codex in `Aire.Core/`
- desktop-specific integrations such as Ollama in `Aire/Providers/`

When adding provider behavior, keep generic protocol and capability logic in `Aire.Core`,
and put Windows- or app-specific behavior in `Aire`.

## Security-Sensitive Areas

Treat these areas as high review priority:
- `Aire/Services/LocalApiService.cs`
- `Aire/Services/ToolExecutionService.cs`
- tool implementations under `Aire.Core/Services/Tools/`
- provider credential persistence in `Aire/Data/Database/`
- browser automation in `Aire/UI/WebViewWindow/`

## Contributor Guidance

If you are making a change:
- add UI layout in the relevant `Controls/` or window XAML
- keep window files focused on UI composition and event forwarding
- prefer `Application` services for workflow logic
- prefer `Domain` for stable contracts and value shapes
- keep infrastructure details behind services, repositories, or gateways
- add tests for behavior changes, especially in service or workflow seams
- use [developer-map.md](./developer-map.md) when deciding where a change belongs
