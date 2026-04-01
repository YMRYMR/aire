# Developer Map

## Where To Change Things

### Main chat and conversations

- UI shell: `Aire/UI/MainWindow.xaml`
- UI coordinators: `Aire/UI/MainWindow/Coordinators/`
- application workflows: `Aire/Application/Chat/`
- persistence: `Aire/Data/Database/`

### Provider selection, setup, and editing

- settings UI: `Aire/UI/SettingsWindow/Providers/`
- onboarding UI: `Aire/UI/OnboardingWindow/`
- application workflows: `Aire/Application/Providers/`
- stable provider contracts: `Aire/Domain/Providers/ProviderContracts.cs`

### Ollama-specific behavior

- onboarding UI: `Aire/UI/OnboardingWindow/Ollama/`
- settings UI: `Aire/UI/SettingsWindow/Providers/SettingsWindow.Models.cs`
- application workflows: `Aire/Application/Providers/` (OllamaAction, OllamaModelCatalog, OnboardingOllama)
- domain contracts: `Aire/Domain/Providers/OllamaContracts.cs`
- infrastructure adapters: `Aire/Services/Providers/OllamaManagementClient.cs`, `Aire/Services/Ollama/`

### Local API

- UI bridge: `Aire/UI/MainWindow/Api/MainWindow.Api.cs`
- application workflow: `Aire/Application/Api/LocalApiApplicationService.cs`
- service host: `Aire/Services/LocalApiService.cs`

### Tool approval and execution

- UI coordinator: `Aire/UI/MainWindow/Coordinators/MainWindow.ToolApprovalCoordinator.cs`
- application workflows: `Aire/Application/Tools/`
- execution/policies: `Aire/Services/` and `Aire/Services/Policies/`

### MCP servers

- settings UI: `Aire/UI/SettingsWindow/Connections/SettingsWindow.McpConnections.cs`
- application workflows: `Aire/Application/Mcp/` (McpStartupApplicationService, McpConfigApplicationService)
- infrastructure: `Aire/Services/Mcp/`

### Email accounts

- settings UI: `Aire/UI/SettingsWindow/Connections/SettingsWindow.EmailConnections.cs`
- application workflows: `Aire/Application/Connections/EmailAccountApplicationService.cs`
- infrastructure: `Aire/Services/Email/`

### Browser and WebView

- UI shell: `Aire/UI/WebViewWindow/`
- tool bridge: `Aire/UI/WebViewWindow/ToolApi/WebViewWindow.ToolApi.cs`

## Where To Add Tests

- service and application workflow tests: `Aire.Tests/Services/ServiceWorkflowRegressionTests.cs`
- local API tests: `Aire.Tests/Services/LocalApiServiceTests.cs`
- UI-adjacent workflow tests: `Aire.Tests/UI/UiWorkflowRegressionTests.cs`

## Rule Of Thumb

- if the change is about rendering, focus, visibility, selection, or progress bars → change `UI`
- if the change is about deciding what should happen next → change `Application`
- if the change is a stable request/result/value shape → prefer `Domain`
- if the change talks to SQLite, HTTP, Ollama, DPAPI, filesystem, WebView, or processes → change `Services` or `Data`
