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
- Read the architecture guides before adding a new feature:
  - [docs/architecture/overview.md](./docs/architecture/overview.md)
  - [docs/architecture/developer-map.md](./docs/architecture/developer-map.md)
  - [docs/architecture/adding-features.md](./docs/architecture/adding-features.md)
  - [docs/providers/how-to-add-a-provider.md](./docs/providers/how-to-add-a-provider.md)
  - [docs/testing/strategy.md](./docs/testing/strategy.md)
  - [docs/testing/manual-smoke-checklist.md](./docs/testing/manual-smoke-checklist.md)
  - [docs/security/model.md](./docs/security/model.md)

## Pull Requests

- Include a short summary of the change.
- Mention any security or migration impact.
- Note how the change was validated.
