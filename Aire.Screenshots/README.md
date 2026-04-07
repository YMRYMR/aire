# Aire.Screenshots

Small Windows utility for capturing repeatable Aire screenshots for docs and help content.

The tool can also automate common WPF UI steps before capture, such as:
- starting Aire
- focusing a window
- clicking buttons by `AutomationId`
- selecting tabs
- waiting for a window to appear

Examples:

```powershell
dotnet run --project .\Aire.Screenshots\Aire.Screenshots.csproj -- list-windows
dotnet run --project .\Aire.Screenshots\Aire.Screenshots.csproj -- capture-window --title-contains "Aire" --process Aire --output ".\tmp\main-window.png" --activate --delay-ms 750
dotnet run --project .\Aire.Screenshots\Aire.Screenshots.csproj -- capture-active --output ".\tmp\active-window.png"
dotnet run --project .\Aire.Screenshots\Aire.Screenshots.csproj -- run-plan --plan ".\Aire.Screenshots\sample-plan.json"
```

Current help assets plan:

```powershell
dotnet run --project .\Aire.Screenshots\Aire.Screenshots.csproj -- run-plan --plan ".\Aire.Screenshots\help-assets-plan.json"
```

Plan shape:

```json
{
  "setupActions": [
    {
      "kind": "wait-for-window",
      "titleContains": "Aire",
      "processName": "Aire",
      "delayMs": 6000
    }
  ],
  "screenshots": [
    {
      "outputPath": "Aire/Assets/Help/main-chat-modes.png",
      "exactTitle": "Aire",
      "processName": "Aire",
      "delayMs": 300,
      "padding": 20,
      "activateWindow": true,
      "actions": [
        {
          "kind": "invoke",
          "automationId": "PART_ModeButton",
          "delayMs": 250
        }
      ]
    }
  ]
}
```

Supported action kinds:
- `start-process`
- `wait`
- `wait-for-window`
- `focus-window`
- `invoke`
- `select`
- `select-combo-item`
- `scroll-into-view`
- `set-active-provider-by-name`
