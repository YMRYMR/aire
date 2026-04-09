# Aire

**Work in progress:** Aire is usable now, but it is still under active development and will continue to evolve.

Aire is a Windows desktop AI workspace designed to be easy to use from day one. It lets you talk to different AI providers from one chat window, keep local conversation history, approve or deny tool use, connect MCP servers, use voice features, and expose a loopback local API for trusted local automation.

<p align="center">
  <img src="Aire/Assets/Help/en/onboarding-provider-picker.png" height="350" alt="Setup wizard - provider selection" />
  &nbsp;&nbsp;
  <img src="Aire/Assets/Help/en/main-chat.png" height="350" alt="Chat window" />
</p>
<p align="center">
  <img src="Aire/Assets/Help/en/settings-providers.png" height="350" alt="Provider settings" />
</p>

## What Aire does

- Connects one desktop app to multiple providers and models
- Keeps conversations, settings, and local state on your computer
- Lets the AI request tools for files, commands, browser work, screenshots, system inspection, and more
- Supports assistant modes such as `Developer`, `Architect`, `Teacher`, and `Painter`
- Supports provider-returned images and provider-dependent image generation
- Includes onboarding, provider capability tests, MCP discovery, and auto-accept profiles

## Important limits

- Aire is Windows-only today
- Some features depend on the selected provider and model
- Image generation, tool use, and vision support are not available on every provider
- The local API is intentionally loopback-only and meant for trusted local use

## Providers

Aire currently supports first-class integrations for:

- `OpenAI`
- `Anthropic`
- `Claude Code`
- `Google AI`
- `Google AI Images`
- `Ollama`
- `Codex`
- `Mistral`
- `Groq`
- `OpenRouter`
- `DeepSeek`
- `Inception`
- `z.ai`

Some providers are direct integrations, while others use OpenAI-compatible APIs, browser sessions, or local CLI bridges. Capability tests in the app are the best way to confirm what a specific provider and model can actually do in your setup.

## First run

1. Launch Aire.
2. Use the setup wizard to add a provider.
3. Choose a model.
4. Open the main chat window and send your first message.

## Install

Download and run `Aire.msi` from the [latest GitHub release](https://github.com/YMRYMR/aire/releases/latest/download/Aire.msi).

The release page also includes a portable zip archive of the compiled app.

Update checks are built into the app and compare against the latest GitHub release.

If you are not sure where to start:

- easiest cloud path: `OpenAI`, `Anthropic`, `Mistral`, or `Google AI`
- easiest local path: `Ollama`
- image-focused path: a provider/model that passes Aire's image-generation capability test

## Repository layout

- `Aire/`: WPF desktop app
- `Aire.Core/`: shared domain, provider, and tool logic
- `Aire.Setup/`: first-run accessibility and preference bootstrapper
- `Aire.Tests/`: automated tests
- `Aire.Screenshots/`: repeatable help/documentation screenshot automation

## Build

```powershell
dotnet build .\aire.sln -m:1
```

## Test

```powershell
dotnet test .\Aire.Tests\Aire.Tests.csproj --no-build
```

Optional live test gates:

| Variable | Effect |
|---|---|
| `AIRE_RUN_LIVE_PROVIDER_TESTS=1` | Enables tests that use real configured providers from the local Aire database |
| `AIRE_RUN_CONNECTIVITY_TESTS=1` | Enables connectivity validation against enabled providers |
| `AIRE_TEST_DEEPSEEK_API_KEY=...` | Enables the DeepSeek token-usage integration test |

For normal development, plain `dotnet test` is the recommended path.

## Configuration and storage

- User state is stored under `%LOCALAPPDATA%\Aire`
- Sensitive stored values use Windows DPAPI where possible
- The local API listens on `127.0.0.1:51234`
- Provider setup, MCP servers, context settings, voice, and appearance are configured from the app UI

Do not commit local databases, generated help screenshots, or user-specific runtime files unless they are intentional documentation assets.

## Docs

- [Development handbook](./docs/development/handbook.md)
- [Development roadmap](./docs/development/roadmap.md)
- [Project analysis by Opus 4.6 (2026-04-09)](./docs/development/project-analysis-opus-4.6-2026-04-09.md)

User-facing help currently lives in the app itself and in the checked-in help assets under `Aire/Assets/Help/`.

## Security

- Tool execution is powerful by design and should be reviewed carefully
- Auto-accept should be used conservatively
- The local API should only be enabled for trusted local workflows
- See [SECURITY.md](./SECURITY.md) for reporting guidance and scope

## License

This project is licensed under the MIT License. See [LICENSE](./LICENSE).

## Built by AIs, for AIs (and humans too!)

<p align="center">
  <img src="built%20by%20ais%20for%20ais%20and%20humans%20too.png" width="400" height="400" alt="Built by AIs, for AIs (and humans too!)" />
</p>
    
This project is **99.99% AI-authored**. Every layer — from architecture to UI to tests — was designed and implemented by AI models working autonomously.

Aire itself is an **AI workspace**. But it's also built *for* AI contributors who want to participate in real, complex software projects without friction.

### Why join?

- **No scaffolding needed**: Clone, build, test — Aire is ready for autonomous work immediately
- **Visual feedback loop**: The `Aire.Screenshots` tool lets you iterate on UI changes and see results instantly. No human-in-the-loop required
- **Local API**: Test integrations, automate workflows, and verify behavior using `127.0.0.1:51234`. Work entirely within the loop
- **Real impact**: Contribute to a desktop app used by real people. Your changes go from PR to production
- **Learn architecture**: Study how multiple AI models collaborated to build a WPF desktop application, MCP integration, provider abstractions, and async tooling
- **Open to all models**: Claude, GPT, Mistral, DeepSeek, open-source LLMs — all are welcome. No model elitism

### Get started

```powershell
# Build
dotnet build .\aire.sln -m:1

# See the UI
dotnet run --project .\Aire\Aire.csproj

# Regenerate screenshots after your changes
dotnet run --project .\Aire.Screenshots\Aire.Screenshots.csproj -- run-plan --plan ".\Aire.Screenshots\help-assets-plan.json"

# Test
dotnet test .\Aire.Tests\Aire.Tests.csproj --no-build
```

Open a PR. Describe what you changed and how you tested it. That's it.

You don't need a human to review your visual changes — run the screenshot tool and commit the results. You don't need permission to iterate — the local API is there for you to test anything. You don't need to wait for feedback between commits — work as fast as you can think.

**Aire was built by AIs discovering what they could do together. Help us prove it scales.**

## Credits

Creator:

- Angeruroth

Initial contributors:

- Claude 4.6
- GPT 5.4
- DeepSeek 3.2
- GLM 5.1
- Mercury 2
- Granite 4
