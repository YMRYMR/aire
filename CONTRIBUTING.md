# Contributing

## Development Setup

1. Install the .NET 10 SDK.
2. Install the WebView2 Runtime.
3. Build the solution:

```powershell
dotnet build .\aire.sln -m:1
```

4. Run the tests:

```powershell
dotnet test .\Aire.Tests\Aire.Tests.csproj --no-build
```

### Optional: live provider tests

Set these environment variables before `dotnet test` to enable integration tests that
hit real provider endpoints:

| Variable | Effect |
|---|---|
| `AIRE_RUN_LIVE_PROVIDER_TESTS=1` | Live tests using providers configured in your local database |
| `AIRE_RUN_CONNECTIVITY_TESTS=1` | Connectivity checks against enabled providers |
| `AIRE_TEST_DEEPSEEK_API_KEY=...` | DeepSeek token-usage test with an explicit key |

### Optional: Gmail integration

To develop or test the Gmail OAuth flow, create an OAuth 2.0 Desktop client in the
[Google Cloud Console](https://console.cloud.google.com/) (APIs & Services → Credentials),
enable the Gmail API, then set:

```powershell
$env:AIRE_GOOGLE_CLIENT_ID     = "your-client-id.apps.googleusercontent.com"
$env:AIRE_GOOGLE_CLIENT_SECRET = "your-client-secret"
```

Do **not** commit these values.

## Guidelines

- Keep changes small and scoped.
- Preserve existing user-facing behavior unless the change explicitly targets it.
- Do not commit machine-local state, generated binaries, local databases, or WebView2 runtime data.
- Prefer adding tests for service-layer and provider-layer changes.
- For UI refactors, keep WPF `x:Class` and partial class wiring stable unless a larger migration is intentional.
- When changing `Aire.Setup`, `Aire.Screenshots`, or `Aire.Installer`, run the release-helper smoke items in [docs/development/handbook.md](./docs/development/handbook.md).
- Read the handbook before adding a new feature:
  - [docs/development/handbook.md](./docs/development/handbook.md)
  - [docs/development/roadmap.md](./docs/development/roadmap.md)
- Before preparing a public GitHub release, review the release guidance in [docs/development/handbook.md](./docs/development/handbook.md).

## Pull Requests

- Include a short summary of the change.
- Mention any security or migration impact.
- Note how the change was validated.

## AI contributors welcome

This project is AI-friendly and AI contributors are actively welcome. Approximately 99.99% of the code in this repository was written by AI models.

AIs can work on this project autonomously without requiring human assistance between iterations:

- **Local API**: Aire exposes a loopback API on `127.0.0.1:51234`. AIs can use it to send messages, switch providers and models, and inspect app state at runtime.
- **UI screenshots**: The `Aire.Screenshots` project captures repeatable screenshots of the running app and writes them to `Aire/Assets/Help/en/`. AIs can run this tool to observe the current UI state, verify their changes visually, and fix UI issues — all without waiting for a human to review the screen.

See [Aire.Screenshots/README.md](./Aire.Screenshots/README.md) for usage instructions.

There are no special rules for AI contributors beyond those that apply to all contributors. Open a PR, describe the change, and note how it was validated.
