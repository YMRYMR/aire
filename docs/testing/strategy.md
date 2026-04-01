# Testing Strategy

## Goals

The test suite should do two things:

- catch regressions that matter to users
- give contributors fast, repeatable feedback before they change behavior

Coverage matters, but only when the tests are behavior-oriented and trustworthy.

Current measured baseline from the latest full coverage run:
- line coverage: `46.43%`
- branch coverage: `37.22%`
- suite: `323` passing tests

## Current testing layers

### 1. Service and provider tests

These are the highest-value tests for most changes.

Focus on:
- request/response handling
- persistence behavior
- recommendation logic
- tool-routing behavior
- provider capability behavior

### 2. UI-adjacent workflow tests

These validate WPF-backed workflows without relying on fragile full-desktop automation.

Focus on:
- settings save/load behavior
- onboarding flow decisions
- help navigation
- local API/state workflows
- security-sensitive UI gating

### 3. Full UI automation

Use sparingly.

These tests are expensive and more fragile. Add them only when a workflow cannot be validated through smaller seams.

### 4. Manual smoke validation

Use the manual checklist before publishing larger UI or workflow changes:
- [manual-smoke-checklist.md]docs/testing/manual-smoke-checklist.md)

## What to prefer

- deterministic inputs
- temp files / temp databases
- mocked HTTP/process boundaries
- narrow workflow tests over reflection-heavy coverage padding

## What to avoid

- tests that only instantiate classes for coverage
- tests that assert implementation trivia without user-visible value
- tests that depend on machine-local state
- live internet dependencies in normal CI

## Commands

Build:

```powershell
dotnet build .\aire.sln -m:1
```

Run tests:

```powershell
dotnet test .\Aire.Tests\Aire.Tests.csproj --no-build
```

Collect coverage:

```powershell
dotnet test .\Aire.Tests\Aire.Tests.csproj --no-build --collect:"XPlat Code Coverage"
```

## Expectations for contributors

When changing behavior, add or update tests that prove:

- the intended path works
- the important failure path is handled
- security-sensitive decisions still hold

If a change is hard to test, that is often a design signal. Prefer extracting a smaller seam rather than leaving the logic buried in UI event handlers.
