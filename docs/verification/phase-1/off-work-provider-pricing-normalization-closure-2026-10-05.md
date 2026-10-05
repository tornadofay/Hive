# Off-Work Provider Completion — Slice 1: Pricing Normalization — 2026-10-05

## Scope

This verification record covers the explicitly authorized off-work **Slice 1: Pricing Normalization** only. It does not advance Phase 1 or authorize Slice 2/3.

The implemented boundary includes:

- explicit pricing source-unit quantity semantics;
- canonical USD per 1M input/output token comparison;
- deterministic currency qualification;
- conservative free-pricing evidence;
- bounded pricing variants with deterministic default handling;
- Model Information consumption of the normalized Core pricing contract;
- focused pricing and Model Information regression coverage;
- owning architecture/plan documentation.

## Developer verification

On 2026-10-05 the developer reran the authoritative automated suite:

```
608 tests
608 passed
0 failed
0 skipped
```

The rerun followed the prior 605/608 result and the bounded Slice 1 remediation for the three reported pricing failures.

The previously failing cases are therefore covered by the successful full-suite result:

- `ProviderPricingNormalizationTests.TopLevelPricingUnitQuantity_AppliesToTokenRates`;
- `ProviderPricingNormalizationTests.ExplicitPricingUnit_DeterminesQuantity`;
- `Phase116FollowUpTests.OpenAICompatibleAdapter_ZeroPricedEntryCountsAsExplicitFreeEvidence`.

## Example Host verification

Exact handoff:

```
Example to run: Providers / Target Selection / Capability Discovery / Provider / Model Information — Hive.Example.WinForms
Tests to run: ProviderPricingNormalizationTests; affected Phase116FollowUpTests; affected ProviderModelMetadataProviderTests; full Hive.Tests
```

The developer manually exercised the exact Model Information scenario with deterministic discovery data:

- Model fixture: `rich-model`;
- Profile sections displayed: Identity / Inputs / Outputs / Capabilities / Reasoning / Thinking / Limits / Pricing / Operational state / Additional provider information;
- Credential: none;
- Durable Model resource created: no;
- External provider call: no.

The Model Information surface opened successfully with the expected deterministic provider/model metadata.

## Verification status

- Source inspected/reviewed: Yes.
- Developer automated tests: **Verified — 608/608 passed, 0 failed, 0 skipped**.
- Build: **Not independently executed by agent**; the developer's successful full test run is the recorded verification evidence.
- Example Host: **Manually verified by developer** for the exact Model Information scenario above.

## Prior remediation history

The first developer verification reported three failures. They were recorded in Active Work as a verification failure before remediation.

The bounded remediation:

- aligned USD-comparison test fixtures with the explicit currency contract;
- restored explicit free evidence for a provider-reported zero input/output token-price pair even when quantity/currency is insufficient for USD comparability;
- preserved conservative behavior for missing quantity and unrelated zero-priced billing dimensions.

The successful 608/608 rerun verifies the corrected behavior.

## Boundary confirmation

This verification does **not** include:

- built-in provider catalog expansion;
- runtime token-usage accounting;
- Phase 1.30 metrics/budgets/OpenTelemetry;
- Agent target-selection changes;
- a durable Model resource;
- background pricing scraping;
- FX conversion;
- unrelated UI/control cleanup;
- roadmap advancement.

## Result

**Slice 1: Pricing Normalization — COMPLETE AND VERIFIED.**

The slice is closed. No new roadmap slice is activated by this record.
