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
- When changing `Aire.Setup`, `Aire.Screenshots`, or `Aire.Installer`, run the release-helper smoke items in [docs/development/testing/manual-smoke-checklist.md](./docs/development/testing/manual-smoke-checklist.md).
- Read the architecture guides before adding a new feature:
  - [docs/development/architecture/overview.md](./docs/development/architecture/overview.md)
  - [docs/development/architecture/developer-map.md](./docs/development/architecture/developer-map.md)
  - [docs/development/architecture/adding-features.md](./docs/development/architecture/adding-features.md)
  - [docs/development/providers/how-to-add-a-provider.md](./docs/development/providers/how-to-add-a-provider.md)
  - [docs/development/testing/strategy.md](./docs/development/testing/strategy.md)
  - [docs/development/testing/manual-smoke-checklist.md](./docs/development/testing/manual-smoke-checklist.md)
  - [docs/development/security/model.md](./docs/development/security/model.md)
- Before preparing a public GitHub release, review [docs/development/release/public-repo-checklist.md](./docs/development/release/public-repo-checklist.md).

## Pull Requests

- Include a short summary of the change.
- Mention any security or migration impact.
- Note how the change was validated.
