# Adding a Provider

## Goal

Providers translate Aire's internal chat/tool model into a concrete backend such as OpenAI, Ollama, Anthropic, or another compatible API.

## Where provider code lives

- `Aire.Core/Providers/`: shared provider implementations and base abstractions
- `Aire/Providers/`: app-specific providers or providers that depend on desktop-only features
- `Aire/Providers/ProviderFactory.cs`: runtime creation and registration

## What a provider needs

At minimum, a provider should define:

1. Metadata
- display name
- provider type id
- field hints for settings/onboarding
- capability flags

2. Initialization
- API key / base URL / timeout / model configuration
- any provider-specific capability setup

3. Chat execution
- `SendChatAsync(...)`
- `StreamChatAsync(...)` if streaming is supported
- `ValidateConfigurationAsync(...)`

4. Tool behavior
- declare tool-calling mode accurately
- use `SharedToolDefinitions` adapters when native tool calling is supported
- if the provider does not support native tool calling, preserve Aire's text-based tool-call behavior

## Recommended workflow

1. Start from the closest existing provider.
- OpenAI-compatible HTTP APIs: start from `OpenAiProvider`
- local Ollama-style APIs: start from `OllamaProvider`
- non-OpenAI tool formats: start from the provider that already matches the target API shape

2. Add or update the provider implementation.

3. Register it in `ProviderFactory`.

4. Make sure metadata is available to settings/onboarding.

5. Add tests before wiring up UI polish.

## Provider design rules

- Keep request/response translation inside the provider.
- Do not reach into WPF UI from provider code.
- Keep provider-specific JSON models private unless they are reused elsewhere.
- Preserve cancellation support.
- Return explicit failures instead of swallowing transport errors.
- Do not put secrets in logs or exceptions unless the value is already non-sensitive.

## Testing expectations

Every provider change should add or update tests for:

- initialization from config
- successful chat response parsing
- failure handling
- tool-call conversion if tools are supported
- streaming behavior if supported

Prefer deterministic tests with mocked HTTP/process boundaries over live network calls.

## Validation checklist

- `dotnet build .\aire.sln -m:1`
- `dotnet test .\Aire.Tests\Aire.Tests.csproj --no-build`
- settings can select/save the provider
- onboarding can configure it if applicable
- provider capabilities match real behavior

## Security notes

- Providers are trusted code and can access user-configured secrets.
- Avoid broad logging of raw request/response payloads.
- Be careful when introducing any capability that executes local tools, reads browser state, or bypasses approval flows.
