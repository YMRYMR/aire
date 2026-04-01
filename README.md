# Aire

Aire is a Windows desktop AI assistant built with WPF and .NET. It provides a chat interface for multiple model providers, local tool execution, browser-assisted workflows, speech input/output, email integrations, and a loopback local API for controlled automation.

## Status

The repository is under active cleanup and refactoring, but it currently builds and tests successfully.

## Features

- Multi-provider chat with OpenAI-compatible, Anthropic, Google, Ollama, Codex, Groq, OpenRouter, DeepSeek, and related providers
- Local tool execution for commands, files, input automation, web fetch, memory, and agent-style workflows
- Embedded browser workflows via WebView2
- Speech recognition and text-to-speech support
- Email account integration and MCP server integration
- Loopback-only local API for external automation
- Onboarding and settings UI for configuring providers and local behavior

## Supported Providers

| Provider type | Notes |
|---|---|
| `OpenAI` | Native OpenAI support; also the base for OpenAI-compatible services |
| `Anthropic` | API-key mode and Claude.ai session flow |
| `GoogleAI` | Gemini models |
| `Ollama` | Local models via the Ollama API |
| `DeepSeek` | OpenAI-compatible |
| `Inception` | OpenAI-compatible |

Any OpenAI-compatible service (Groq, OpenRouter, custom endpoints) can be configured using the `OpenAI` provider type with a custom base URL.

## Repository Layout

- `Aire/`: WPF desktop application
- `Aire.Core/`: shared provider and tool logic
- `Aire.Tests/`: automated test suite
- `docs/`: refactor notes and project documentation

## Architecture

- [docs/architecture/overview.md](./docs/architecture/overview.md)
- [docs/architecture/developer-map.md](./docs/architecture/developer-map.md)
- [docs/architecture/adding-features.md](./docs/architecture/adding-features.md)
- [docs/providers/how-to-add-a-provider.md](./docs/providers/how-to-add-a-provider.md)
- [docs/testing/strategy.md](./docs/testing/strategy.md)
- [docs/testing/manual-smoke-checklist.md](./docs/testing/manual-smoke-checklist.md)
- [docs/security/model.md](./docs/security/model.md)
- [docs/release/public-repo-checklist.md](./docs/release/public-repo-checklist.md)

## Requirements

- Windows 10 version 1809 or later
- .NET 10 SDK
- WebView2 Runtime

## Build

```powershell
dotnet build .\aire.sln -m:1
```

## Test

```powershell
dotnet test .\Aire.Tests\Aire.Tests.csproj --no-build
```

The default run is stable on any development machine. Live provider tests are opt-in:

| Variable | Effect |
|---|---|
| `AIRE_RUN_LIVE_PROVIDER_TESTS=1` | Enables integration tests using real configured providers from the local Aire database |
| `AIRE_RUN_CONNECTIVITY_TESTS=1` | Enables connectivity validation against enabled providers |
| `AIRE_TEST_DEEPSEEK_API_KEY=...` | Enables the DeepSeek token-usage test with an explicit API key |

Live tests depend on your local `%LOCALAPPDATA%\Aire\aire.db` and external provider availability. For normal development, plain `dotnet test` is the recommended path.

## Coverage

```powershell
dotnet test .\Aire.Tests\Aire.Tests.csproj --collect:"XPlat Code Coverage"
```

Latest measured baseline:
- line coverage: `46.43%`
- branch coverage: `37.22%`
- passing tests: `323`

## Configuration

Most end-user configuration is done through the app UI.

- Provider credentials are configured in `Settings`
- Sensitive stored values use Windows DPAPI where supported
- Local app state is stored under `%LOCALAPPDATA%\Aire`
- The local API binds to loopback and uses a local token

Do not commit local databases, generated WebView2 data, or user-specific config files.

## Security Notes

- The local API is intended for trusted local use only
- Tool execution features are powerful by design and should be reviewed before exposing them to broader automation
- See [SECURITY.md](./SECURITY.md) for reporting guidance and security scope

## Contributing

See [CONTRIBUTING.md](./CONTRIBUTING.md).

## License

This project is licensed under the MIT License. See [LICENSE](./LICENSE).
