# Security Model

## Trust boundaries

### 1. The desktop app

The Aire desktop process is trusted application code. It can:
- read user-configured provider settings
- access local state
- execute approved tools
- open browser/email/system integrations

### 2. AI providers

Providers are not automatically trusted with unlimited local execution.

They can generate tool requests, but actual local actions must still pass through Aire's tool execution and approval boundaries.

### 3. Local API clients

The local API is loopback-only and token-protected. It is intended for trusted local automation, not remote or multi-user exposure.

## Security-sensitive areas

### Tool execution

Tool execution is powerful by design. Changes here must preserve:
- approval requirements
- session boundaries
- logging restraint
- explicit path/command handling

Relevant code includes:
- `ToolExecutionService`
- tool approval policy/coordinators
- command, browser, input, filesystem, and system tool services

### Secret storage

Secrets should be stored with DPAPI-backed protection where applicable.

Be careful when changing:
- provider credential persistence
- OAuth refresh tokens
- local API token storage
- any remembered or cached sensitive value

### Local API

The local API must not bypass the same approval/security expectations as the main app workflows.

When changing local API behavior, verify:
- authentication still applies
- loopback-only assumptions still hold
- powerful tool paths do not bypass approval logic
- trace logging does not leak secrets

### Browser and automation features

Browser scripting, cookies, keyboard, mouse, clipboard, and command execution are high-impact features. Treat all changes here as security-relevant.

## Contributor rules

- Do not log raw secrets.
- Prefer explicit allow/deny behavior over vague heuristics.
- Keep compatibility fallbacks narrow and temporary.
- Add tests for any security-sensitive behavioral change.
- If a change weakens an approval boundary, document it explicitly in the PR.

## Review checklist

Before merging security-relevant changes, check:

- does this broaden who can trigger a local action?
- does this broaden what data gets stored or logged?
- does this bypass an existing approval or token check?
- does this make a previously local-only feature remotely reachable?
- is there a regression test for the changed boundary?
