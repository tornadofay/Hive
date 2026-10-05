# Maintenance — Provider Adapter Decomposition, Model Filter Extraction & Boundary-Rule Corrections — Closure — 2026-10-05

## Result

**Complete and verified.**

The four authorized corrective items are complete within the bounded off-roadmap maintenance boundary recorded in `docs/Hive_Active_Work.md`. This work did not activate or advance any Phase 1 roadmap slice, did not change Agent target selection, and did not change provider transport semantics, model-catalog parsing semantics, or the persisted model-metadata contract.

Checkpoint: `680c322` — "Close verified Slice 4 Revision".

## Developer verification

Developer verification on 2026-10-05:

```
663 Tests
663 Passed
0 Failed
0 Skipped
```

Run duration: approximately 1.3 minutes.

This matches the agent-observed final state exactly. The pre-change baseline on the same checkpoint was 638 passed, so the 25 added tests account for the difference and no pre-existing test regressed.

## Example Host verification

Developer manually exercised the required scenario:

**Example to run:** `Provider / Model Information` — Hive.Example.WinForms

Reported:

- Advanced Provider Configuration opened with deterministic discovery data;
- selection: Model Information;
- model fixture: `rich-model`;
- profile sections: Identity / Inputs / Outputs / Capabilities / Reasoning / Thinking / Limits / Pricing / Operational state / Additional provider information;
- credential: none;
- durable Model resource created: no;
- external provider call: no.

The developer additionally exercised the provider-completion scenario, `Provider Completion Integration & Hardening`, which re-confirmed the previously verified completion evidence: pricing evidence attached with Provider/Account/endpoint provenance, input 0.35 and output 1.50 USD / 1M tokens, 1 pricing variant preserved, provider-reported Actual usage of 120 input / 45 output / 165 total, configured versus provider-reported model identity separation, pricing and usage persisted with the terminal execution event, no execution-time discovery, no external provider call, no credentials, and migration schema 14 applied.

The provider-completion result is the relevant manual regression evidence for item 4: it exercises the OpenAI-compatible transport and model-discovery paths that were decomposed, and it retained its previously verified values exactly.

## Authorized items verified

1. **Model Information filter extraction.** `ModelInformationFilter` in `Hive.Host.WinForms` is now the single owner of the filter rule and of the free-model decision, with `ModelFilterCriteria` carrying no control reference. `HiveModelInformationSettingsView` is a thin adapter that snapshots control state and delegates. The Model Information page rendered all expected profile sections with deterministic data and no external provider call.
2. **`HiveCrudPage.ListOnKeyDown` guard.** Both awaited branches contain their failures and route to `HiveUiErrorReporter`; the reporter remains a contained observer boundary. A sweep confirmed this was the only unguarded `async void` in `Hive.Host.WinForms.UI`.
3. **Public-error boundary.** All six `exception.Message` sites in `InputPreparationEngine` now resolve through `InputPreparationFailureCatalog` (41 registered spreadsheet codes); an unregistered code resolves to a category-appropriate generic message rather than exception detail. Zero `exception.Message` forwarding remains in `src`.
4. **Provider adapter decomposition.** `OpenAICompatibleProviderAdapter.cs` is transport-only (3,040 → 987 lines). `Hive.Providers.OpenAICompatible.ModelCatalog` owns `IModelCatalogParser`, `ModelCatalogParserRegistry`, `ModelMetadataNormalizer`, and one parser per catalog format. The dispatch switch became a fail-closed registry lookup.

## Regression coverage

`OpenAICompatibleProviderAdapterTests` (39 tests) passed unchanged. `ProviderPricingNormalizationTests` (14), `Phase116FollowUpTests` (19), `ProviderCompletionIntegrationTests`, and `ProviderModelMetadataProviderTests` all passed within the full run. The existing Model Information filter assertions in `Phase116FollowUpTests` — including the $0/explicit-free view, per-token price normalization, and unknown-quantity handling — continued to hold without modification.

## Build evidence

`dotnet build Hive.sln` was executed by the agent against this exact source state and reported 0 errors and 0 warnings. No separate developer-supplied build result was reported.

## Verification boundary

No claim is made for manual verification of filter interaction sequences beyond the reported Model Information page render; the price-slider and capability-filter interaction behavior is covered by the 20 direct `ModelInformationFilterTests` and the pre-existing `Phase116FollowUpTests` WinForms assertions rather than by reported manual stepping.

Three initial agent-authored test expectations were wrong during implementation (double-based `InlineData` losing decimal precision, an inverted minimum-boundary case, and `ArgumentNullException` versus `ArgumentException` for blank input). Those were defects in the newly written tests, not in production code, and were corrected before the final passing run that produced the verified 663/663 result.

No Phase 1 roadmap slice was activated or advanced by this maintenance work.