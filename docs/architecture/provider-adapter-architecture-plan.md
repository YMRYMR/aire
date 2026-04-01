# Provider Adapter Architecture Plan

## Goal

Standardize the provider stack around one clear rule:

- `Aire` owns the product semantics.
- each provider owns the adapter strategy used to satisfy those semantics.

This is the next architectural step after the layering work. The point is not to
force every provider to behave internally like OpenAI. The point is to stop
provider quirks from leaking into the rest of the app while still letting each
provider integrate in the most natural and reliable way.

## Core Principle

The app should not ask:

- "How do we force every provider to work exactly the same way?"

It should ask:

- "How does this provider best implement Aire's shared contract?"

That means:

- `Aire semantics` stay shared and stable.
- `provider adapters` translate those semantics into provider-specific runtime behavior.

## What Aire Must Own

These behaviors are product-level semantics and should remain provider-independent:

- conversation lifecycle
- message persistence
- memory persistence and recall rules
- local API contract
- tool approval and denial model
- audit trail and security logging
- enabled tool categories
- workflow outcomes such as:
  - normal assistant text
  - tool call requested
  - follow-up question
  - attempt completion
  - switch model
  - update todo list

These must not drift per provider.

If one provider bypasses these rules, the app becomes inconsistent, harder to
secure, and harder to test.

## What Providers Should Own

These behaviors should be adapter-specific:

- request packing
- role mapping
- system prompt strategy
- tool-call encoding and decoding
- native tool schema support
- text-based tool-call fallbacks
- model capability quirks
- retries and timeout strategy
- session reuse / process reuse
- provider-specific validation logic
- provider-specific install / login / runtime diagnostics

This is where flexibility belongs.

## Architectural Shape

Target stack:

- `UI`
  - render state
  - collect user input
  - show dialogs, progress, approvals
- `Application`
  - shared workflows
  - shared provider orchestration
  - persistence decisions
  - tool approval semantics
- `Domain`
  - provider-independent contracts and result shapes
  - capability flags
  - shared tool intent model
  - shared workflow outcomes
- `Infrastructure`
  - provider adapters
  - HTTP clients
  - CLI bridges
  - browser bridge
  - database / DPAPI / file access

For providers specifically:

- `Application` should talk to a stable provider adapter contract
- `Infrastructure` should implement one adapter per provider family

## Proposed Contracts

### 1. Shared Aire semantic contracts

These should live in `Domain` or `Application` depending on stability.

Suggested contracts:

- `ProviderRequestContext`
  - normalized conversation/messages
  - selected model
  - enabled tool categories
  - shared system rules
  - timeout/cancellation

- `ProviderExecutionResult`
  - normalized assistant text
  - tool request intent
  - workflow intent
  - raw provider diagnostics if needed

- `ToolIntent`
  - shared representation of a requested Aire tool call
  - independent from OpenAI/native/Hermes/Codex formats

- `WorkflowIntent`
  - `AssistantText`
  - `ToolCall`
  - `FollowUpQuestion`
  - `AttemptCompletion`
  - `SwitchModel`
  - `UpdateTodoList`

- `ProviderValidationOutcome`
  - valid / invalid
  - normalized failure reason
  - remediation guidance

### 2. Provider adapter interface

Suggested interface shape:

- `IProviderAdapter`
  - `ProviderKind`
  - `SupportsNativeTools`
  - `SupportsImages`
  - `SupportsSystemMessages`
  - `SendAsync(ProviderRequestContext)`
  - `ValidateAsync(ProviderValidationRequest)`
  - `ListModelsAsync(...)` where relevant
  - `GetDiagnostics()` where relevant

This should replace the current situation where some providers are effectively
"full runtime engines" and others are "OpenAI-like with overrides".

### 3. Provider family adapters

Likely grouping:

- `OpenAiCompatibleAdapterBase`
  - OpenAI
  - z.ai
  - DeepSeek
  - Groq
  - OpenRouter
  - Inception

- `GoogleAiAdapter`

- `OllamaAdapter`

- `CodexCliAdapter`

- `ClaudeWebAdapter`

The point is not to flatten everything to one base class. The point is to reuse
transport families where they are real, and let special providers stay special.

## Codex As The Current Example

Codex exposed the problem clearly:

- Aire wants shared tool approval, persistence, and workflow semantics.
- Codex wants to behave like an agent with its own CLI/runtime assumptions.

The right fix is not:

- "make Codex pretend to be OpenAI"

The right fix is:

- keep Aire semantics shared
- give Codex a dedicated adapter that speaks Codex naturally

That adapter may need:

- process/session reuse
- a Codex-specific prompt packer
- a Codex-specific tool parser
- a Codex-specific validation flow
- a stricter execution envelope than the current generic provider path

But it should still return shared Aire intents.

## Migration Plan

### Phase 1. Define the shared provider-semantic contract

Add the missing shared contracts first:

- `ProviderRequestContext`
- `ProviderExecutionResult`
- `ToolIntent`
- `WorkflowIntent`
- `ProviderValidationOutcome`

Do not move providers yet.

Definition of done:

- these contracts exist
- they are independent from UI and provider transport details
- tests cover serialization/normalization behavior where relevant

### Phase 2. Add the adapter interface

Introduce `IProviderAdapter` and a small adapter coordinator in the
application layer.

The application side should stop reasoning in terms of:

- raw provider quirks
- raw OpenAI shapes
- provider-specific parsing scattered across workflows

Definition of done:

- application workflows depend on adapter outputs, not provider transport shapes

### Phase 3. Migrate provider families

Do this in order:

1. `OpenAI-compatible family`
   - highest reuse
   - easiest place to prove the abstraction

2. `Ollama`
   - local/native quirks
   - model/hardware metadata already structured

3. `Codex`
   - highest runtime quirk value
   - should become a first-class dedicated adapter

4. `ClaudeWeb`
   - highly specialized browser/session integration

5. `Google AI`
   - separate schema/tool details

Definition of done:

- each migrated provider returns shared intents/results
- shared workflows no longer branch on provider quirks as often

### Phase 4. Simplify shared workflows

Once adapters exist, revisit:

- `ChatTurnWorkflowService`
- provider runtime/application services
- capability testing flow

These should operate on shared intent types instead of format-specific parsing
rules spread across providers.

Definition of done:

- shared workflows are smaller
- provider-specific behavior is pushed back into adapters

### Phase 5. Raise coverage against the new seam

Once the seam is real:

- add provider-adapter contract tests
- add per-adapter behavior tests
- add workflow tests that use fake adapters

This is the fastest path toward the long-term coverage goal without padding.

## Immediate Candidate Refactors

### 1. Extract a real `CodexCliAdapter`

This is the best next proving ground because the current Codex issues are mostly
adapter issues, not Aire-semantics issues.

Responsibilities:

- CLI detection
- install/login diagnostics
- prompt packing
- process/session execution
- tool-intent decoding
- validation

Output:

- shared `ProviderExecutionResult`

### 2. Separate OpenAI-compatible parsing from app workflows

Today too much of the provider parsing model still assumes OpenAI-like behavior.
That should move into the OpenAI-compatible adapter family.

### 3. Move capability testing onto adapters

Capability testing currently has too much provider-format awareness in shared
service code. The shared service should define the test intent, and the adapter
should decide how to express it for that provider.

## What Not To Do

- do not let providers own approval policy
- do not let providers write directly to persistence for chat semantics
- do not let provider adapters define their own local API behavior
- do not invent many tiny abstractions that add no seam
- do not rewrite all providers at once

## Success Criteria

This refactor is successful when:

- the app has one shared semantic model for provider outcomes
- each provider implements a clear adapter around that model
- provider quirks stop leaking into shared workflows
- Codex, Ollama, Claude Web, and OpenAI-compatible providers can all stay
  different internally without making the rest of the app messy
- coverage becomes easier to raise on truthful behavior seams

## Recommended First Implementation Slice

1. Add the shared contracts.
2. Add `IProviderAdapter`.
3. Implement `CodexCliAdapter` first.
4. Move the current Codex runtime path behind that adapter.
5. Validate live behavior.
6. Then migrate the OpenAI-compatible family.

That is the smallest slice that proves the architecture with a real provider
that clearly needs its own adapter behavior.
