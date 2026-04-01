# Adding Features Safely

## General Rules

- Do not put new business logic directly into a large window file if it can live in a service or coordinator.
- Keep WPF `x:Class` files stable unless the change really requires a XAML-level move.
- Prefer extending an existing feature folder over creating unrelated helper files at the project root.

## Where New Code Usually Belongs

### New UI-only element

Put it under the relevant window `Controls/` folder.

Examples:
- [Aire/UI/MainWindow/Controls]Aire/UI/MainWindow/Controls)
- [Aire/UI/SettingsWindow/Controls]Aire/UI/SettingsWindow/Controls)

### New workflow or user action flow

Put it in a coordinator or service, not directly in XAML code-behind.

Preferred places:
- [Aire/UI/MainWindow/Coordinators]Aire/UI/MainWindow/Coordinators)
- [Aire/Services]Aire/Services)
- [Aire.Core/Services]Aire.Core/Services)

### New provider capability

Start in:
- [Aire.Core/Providers]Aire.Core/Providers)

If the provider needs Windows- or app-host-specific behavior, add that in:
- [Aire/Providers]Aire/Providers)

### New persisted setting or record

Update:
- [Aire/Data/DatabaseService.cs]Aire/Data/DatabaseService.cs)
- partials under [Aire/Data/Database]Aire/Data/Database)

## Review Checklist

Before opening a PR:
- build the solution
- run the tests
- confirm whether the change touches any security-sensitive path
- note any migration impact
- avoid leaving misleading names behind when a class/file has changed purpose

## Naming Guidance

- Use names that describe responsibility, not historical origin.
- Avoid names that imply a single theme, provider, or UI mode when the code is now generalized.
- Prefer `AppTheme`, `ProviderCoordinator`, `ConversationCoordinator`, `HardwareSummary`, and similar names over legacy labels that no longer match behavior.

## Current Good Seams To Extend

- [AppearanceService.cs]Aire/Services/AppearanceService.cs)
- [OllamaService.Recommendations.cs]Aire/Services/Ollama/OllamaService.Recommendations.cs)
- [MainWindow.ChatCoordinator.cs]Aire/UI/MainWindow/Coordinators/MainWindow.ChatCoordinator.cs)
- [MainWindow.ProviderCoordinator.cs]Aire/UI/MainWindow/Coordinators/MainWindow.ProviderCoordinator.cs)
- [SettingsWindow.ProviderForm.cs]Aire/UI/SettingsWindow/Providers/SettingsWindow.ProviderForm.cs)

## Areas That Still Need Extra Care

- WPF-heavy files with lots of event wiring
- local API and tool-execution flows
- provider request/response normalization
- large shared metadata files such as tool definitions

