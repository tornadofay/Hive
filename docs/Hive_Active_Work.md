# Hive — Active Work

Status: NO ACTIVE WORK

## Maintenance — Provider Adapter Decomposition, Model Filter Extraction & Boundary-Rule Corrections

**Closed and verified 2026-10-05.** [Verification record](verification/phase-1/maintenance-adapter-decomposition-model-filter-boundary-rules-closure-2026-10-05.md)

Authorized: 2026-10-05 (explicit maintainer request in a dedicated chat)
Checkpoint: `680c322` — "Close verified Slice 4 Revision"
Roadmap impact: None. This was bounded off-roadmap corrective/maintenance work. It did not activate or advance any Phase 1 roadmap slice, and it did not change Agent target selection, provider transport semantics, model-catalog parsing semantics, or the persisted model-metadata contract.

### Authorized scope (as implemented)

Four previously identified production defects. All four were corrective: they preserve or restore existing verified behavior, remove an inconsistency between two boundaries, or decompose an existing implementation behind unchanged contracts.

1. **Model Information filter extraction (`Hive.Host.WinForms`)** — complete. `ModelInformationFilter` + `ModelFilterCriteria` own the rule and the free-model decision with no control reference; the view is a thin adapter; 20 focused tests added.
2. **Unguarded `async void` at `HiveCrudPage.ListOnKeyDown` (`Hive.Host.WinForms.UI`)** — complete. Both awaited branches route to `HiveUiErrorReporter` through a contained observer boundary. Confirmed the only unguarded `async void` in that project.
3. **`exception.Message` reaching public `Error.Message` (`Hive.Core/Input`)** — complete. `InputPreparationFailureCatalog` owns 41 spreadsheet failure codes; all six sites resolved through it; zero `exception.Message` forwarding remains in `src`.
4. **Provider adapter decomposition (`Hive.Providers.OpenAICompatible`)** — complete. Adapter is transport-only (3,040 → 987 lines); `ModelCatalog/` owns `IModelCatalogParser`, `ModelCatalogParserRegistry`, `ModelMetadataNormalizer`, and one parser per format; the dispatch switch became a fail-closed registry lookup. Verified as a pure move by normalized diff.

### Verification

Developer verification: full `Hive.Tests` suite **663/663 passed** (0 failed, 0 skipped) in ~1.3 minutes, matching the agent-observed state. `Hive.Example.WinForms` `Provider / Model Information` was manually exercised and reported all expected profile sections with deterministic data, no credential, no durable Model resource, and no external provider call.

No further implementation authorization is open. Phase 1.20+ remains unauthorized.