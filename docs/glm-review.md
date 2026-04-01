---

# Aire — Project Quality Report

## 1. Executive Summary

Aire is a **well-organized, mid-maturity WPF desktop application** (~378 source files across C# and XAML) that has already undergone significant architectural improvement. The codebase sits in a transitional state: it is cleanly *organized* by feature but not yet fully *layered* by concern. The documentation is unusually strong for a project this size, with clear improvement plans, security models, and contributor guidance. The test suite (342 passing tests) provides meaningful coverage at the service and workflow layers. The two remaining items on the improvement plan are high-value but non-urgent.

**Overall grade: B+** — strong foundations, clear trajectory, a few structural knots to untie.

---

## 2. Architecture Assessment

### What's working well

**Clear folder conventions.** The `Aire/UI/MainWindow/` structure (split into `Api/`, `Shell/`, `Conversations/`, `Providers/`, `Chat/`, `Speech/`, `Search/`, `Coordinators/`, `Controls/`, `Resources/`) is exactly what the refactor plan prescribed, and it's been executed. Each window follows the same pattern — `SettingsWindow/`, `HelpWindow/`, `OnboardingWindow/` all use feature subfolders.

**Application layer is real.** The `Aire/Application/` directory contains 30+ application services covering chat, providers, tools, MCP, email, settings, and API workflows. These are not hollow wrappers — they own orchestration, normalization, and decision logic. The abstraction interfaces in `Application/Abstractions/` (8 ports for repositories and gateways) are narrow and purposeful.

**Provider model is well-designed.** The split between `Aire.Core/Providers/` (shared, protocol-level) and `Aire/Providers/` (desktop-specific, e.g. ClaudeWeb, Ollama) respects the right boundary. The `ProviderContracts.cs` / `OllamaContracts.cs` / `CodexContracts.cs` domain shapes are stable and well-named. The `ProviderValidationResult` with `Ok()`/`Fail(string)` factory methods is a good pattern — the fix we applied today was completing a migration that was already planned.

**Security model is documented and followed.** The security doc correctly identifies trust boundaries, names specific files, and has actionable contributor rules. The hardcoded OAuth credential fix (item 1.1) and the verification sweep (item 1.2) show this isn't just theory.

### What needs attention

**MainWindow is still a god-class.** Despite the partial extraction into `Coordinators/`, `MainWindow` still holds ~20+ injected service dependencies (`_databaseService`, `_providerFactory`, `_chatSessionApplicationService`, `_conversationApplicationService`, `_toolExecutionService`, `_speechService`, `_localApiApplicationService`, `_toolApprovalPromptApplicationService`, `_toolApprovalExecutionApplicationService`, `_availabilityTracker`, `_settingsWindow`, etc.). The coordinators reduce line count but don't reduce coupling — `MainWindow` is still the composition root for nearly every service in the app. Item 2.4 in the improvement plan calls this out correctly.

**Domain layer is thin.** `Aire.Core/Domain/Providers/` contains only 3 contract files. The actual domain concepts (capabilities, tool definitions, approval policies, conversation abstractions) are scattered across `Aire.Core/Data/`, `Aire.Core/Services/`, and `Aire/Application/`. The migration plan (Phase 4) acknowledges this. Currently the "Domain" label is aspirational rather than structural.

**`Aire` project is a monolith layer.** The `Aire/` project contains UI, Application services, Infrastructure services, Data access, and Provider implementations all in one project. There's no compile-time enforcement of dependency direction — a UI coordinator can directly reference `DatabaseService` or `OllamaManagementClient` without any build error. The interface extraction (item 5.2) mitigates this at the type level, but the physical project boundary doesn't enforce it.

**Coordinators blur UI and Application.** Files like `MainWindow.ChatCoordinator.cs` and `MainWindow.ToolApprovalCoordinator.cs` are positioned as UI layer but contain workflow branching logic (retry decisions, error handling cascades, tool execution loops) that arguably belongs in Application services. The migration plan's Phase 3 correctly identifies this.

---

## 3. Code Quality

### Strengths

- **Named constants and magic value elimination.** Items 4.1 and 4.2 are done. `Provider.DefaultTimeoutMinutes`, `LocalApiService.Port` — no raw literals leaking through.
- **Logging is in place.** `AppLogger` exists and silent catches have been audited (item 3.1). The remaining silent catches are annotated with justification comments.
- **Boolean trap eliminated.** `SetSidebarOpen(bool)` → `OpenSidebar()`/`CloseSidebar()` (item 5.1).
- **DatabaseService interface split.** Six narrow interfaces instead of one God dependency (item 5.2).
- **ProviderFactory deduplication.** Single dictionary dispatch (item 2.2).

### Weaknesses

- **20 nullable field warnings on `MainHeaderControl` test fields.** These `_test*` fields exist purely for test injection via reflection but are declared as non-nullable. This is a code smell — the test infrastructure is fighting the type system instead of working with it. Marking them nullable (`_testProviderComboBox?`) would be a one-line fix per field.
- **Duplicate `using Aire.Services` in `HelpWindow.Shell.cs`.** Minor but indicates copy-paste drift.
- **`RuntimeHelpers.GetUninitializedObject` in tests.** Used in 5+ test methods to create WPF objects without running constructors. This is necessary but fragile — if a constructor adds a required field, tests silently get null refs (exactly what happened in the two UI tests we fixed). This is an inherent risk of testing WPF without a proper DI container.

---

## 4. Test Quality

### Strengths

- **342 tests, all passing.** Good baseline for a desktop app.
- **Service workflow tests are the backbone.** `ServiceWorkflowRegressionTests.cs` and `LocalApiServiceTests.cs` test real orchestration paths with temp databases, not just trivial getters.
- **Provider coverage tests.** `AppProviderCoverageTests.cs`, `GoogleAiProviderTests.cs`, `OpenAiProviderTests.cs` validate initialization, metadata, and validation for each provider type.
- **UI workflow tests are pragmatic.** They use STA threads and `RuntimeHelpers.GetUninitializedObject` to test WPF-backed logic without full desktop automation. The test base (`TestBase.cs`) centralizes the STA threading pattern.
- **Testing strategy doc is honest.** It explicitly says to avoid "tests that only instantiate classes for coverage" and "tests that assert implementation trivia without user-visible value."

### Weaknesses

- **46% line coverage / 37% branch coverage** (from the strategy doc baseline, likely now slightly higher after adding tests). This is acceptable for a desktop app but means many error paths and edge cases are untested.
- **No tests for Application services directly.** The 30+ application services in `Aire/Application/` are tested indirectly through UI workflow tests and service regression tests, but none have dedicated unit test files. If item 2.4 (coordinator extraction) is executed, the resulting services will need direct tests.
- **`ProviderConnectivityTests.cs` is opt-in** via `AIRE_RUN_CONNECTIVITY_TESTS=1`. This is correct for CI but means live provider validation is rarely exercised.
- **Two test files had missing service injections.** The fact that `MainWindowTests` was missing `_providerFactory` and `UiWorkflowRegressionTests` was missing `_appSettingsApplicationService` suggests these tests were written against the old architecture and not updated when the application services were introduced. This is a maintenance debt signal.

---

## 5. Documentation Quality

This is the project's standout strength.

- **`docs/architecture/overview.md`** — concise, accurate, tells you exactly where each concern lives.
- **`docs/architecture/developer-map.md`** — "where to change things" + "rule of thumb" is exactly what a new contributor needs.
- **`docs/improvement-plan.md`** — every item has status, file reference, acceptance criterion, and "next step". This is unusually well-maintained.
- **`docs/architecture/layered-architecture-migration-plan.md`** — honest about current state ("organized but not yet layered"), clear about target, phased migration with risk mitigations.
- **`docs/refactors/MainWindowRefactorPlan.md`** and **`AppRefactorContinuationPlan.md`** — concrete extraction orders with safety rules.
- **`docs/security/model.md`** — trust boundaries, sensitive areas, contributor rules, review checklist.
- **`docs/providers/how-to-add-a-provider.md`** — step-by-step with design rules and testing expectations.
- **`docs/testing/strategy.md`** — goals, layers, preferences, anti-patterns.

The only gap: no ADR (Architecture Decision Record) log. Decisions like "why `ProviderValidationResult` over exception" or "why partial classes over MVVM" are implicit in the improvement plan but not formally recorded. This is a nice-to-have, not a need.

---

## 6. Security Assessment

- **OAuth credentials externalized.** ✅ Done via environment variables.
- **No secrets in source.** ✅ Verified by grep.
- **Tool execution has approval boundaries.** ✅ Documented and tested.
- **Local API is loopback + token protected.** ✅ Documented.
- **Secret storage uses DPAPI.** ✅ Documented as target pattern.
- **Risk: `ProviderValidationResult.Fail(error)` messages could leak connection details.** The error messages we added today (e.g., `"Ollama returned HTTP 503"`, `"Model 'x' not found"`) are useful for debugging but are surfaced in the chat UI per item 3.2. If the chat transcript is logged or shared, these contain infrastructure details. Low risk but worth noting.

---

## 7. Remaining Improvement Plan Items

| Item | Description | Impact | Effort |
|------|-------------|--------|--------|
| 2.4 | MainWindow god-class decomposition | High — reduces coupling, enables direct service testing | High — 3 coordinators to extract, each with dependency wiring |
| 3.2 | Surface configuration errors to user | Medium — `ProviderValidationResult` exists but UI doesn't display `Error` | Low — mostly UI wiring, error message is already available |

Both items are correctly identified and prioritized. Item 3.2 is the natural next step — it's low effort and completes the `ValidateConfigurationAsync` migration we finished today.

---

## 8. Structural Risks

1. **Single-project layering.** Without physical project boundaries, nothing prevents a future contributor from adding a `using Aire.Data` in a UI coordinator. The interfaces help but don't enforce. This is the biggest long-term architectural risk.

2. **`MainWindow` as implicit composition root.** Every new feature adds another `_service` field to `MainWindow`. Without a formal DI container or at least a builder pattern, this will continue to grow. The coordinator extraction (2.4) helps but doesn't fully address it.

3. **Test fragility from `GetUninitializedObject`.** The two test failures we fixed today were caused by new service dependencies not being injected into uninitialized objects. This pattern will break again whenever a constructor or service dependency changes. The mitigation is to test application services directly rather than through WPF objects.

4. **Domain layer is a label, not a boundary.** `Aire.Core/Domain/` contains 3 files. Most domain concepts live in `Aire.Core/Data/` (which contains entity models) and `Aire.Application/` (which contains workflow logic that should arguably be domain rules). This makes the architecture docs slightly aspirational.

---

## 9. Recommendations (Priority Order)

1. **Complete item 3.2** — Wire `ProviderValidationResult.Error` into the chat/status bar UI. This is a small, high-value change that closes out the `ValidateConfigurationAsync` migration.

2. **Add nullable annotations to `MainHeaderControl` test fields** — 8 one-line fixes, eliminates warnings, reduces cognitive noise.

3. **Remove duplicate `using` in `HelpWindow.Shell.cs`** — trivial but prevents warning fatigue.

4. **Start item 2.4 with `ChatCoordinator` extraction** — This is the highest-coupling coordinator. Extract the chat turn loop and streaming logic into `ChatSessionCoordinator` as an application service with direct tests.

5. **Add dedicated tests for `Application/` services** — At minimum, `ChatTurnApplicationService`, `ToolApprovalExecutionApplicationService`, and `ProviderActivationApplicationService` should have direct unit tests independent of WPF.

6. **Begin physical project split planning** — Not execution, but mapping: which `Aire/Services/` files would move to `Infrastructure`, which `Aire.Core/Data/` models are domain vs. infrastructure. Update the migration plan with a file-level inventory.

---

## 10. Summary Scores

| Dimension | Score | Notes |
|-----------|-------|-------|
| Architecture | B+ | Clean organization, real application layer, but no physical layer enforcement |
| Code quality | B | Good patterns, named constants, logged errors; nullable warnings and test fragility drag it down |
| Testing | B- | Good workflow coverage, but low overall coverage and no direct app-service tests |
| Documentation | A | Best aspect of the project — clear, honest, actionable, well-maintained |
| Security | A- | Credentials externalized, boundaries documented, approval logic tested; minor info-leak risk in error messages |
| Maintainability | B | Feature folders make navigation easy, but `MainWindow` coupling and test fragility create maintenance cost |
| **Overall** | **B+** | Solid project with clear improvement trajectory and unusually good documentation |