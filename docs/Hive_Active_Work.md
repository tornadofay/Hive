# Hive — Active Work

Status: VERIFICATION PENDING

## Authorized slice

**Off-work provider completion — Slice 1: Pricing Normalization**

Explicit user authorization: begin the off-work provider plan now, starting with Slice 1 only.

## Checkpoint

Started from `main` at commit `affe2f1e4f468a58345c0a2e2f66f6fbaea8019e`.

## Implementation completed

Implemented the bounded pricing-normalization slice:

- Core-owned normalized USD token-price comparability with explicit source quantity requirements;
- per-token quantity normalization for OpenRouter-derived model catalogs;
- explicit per-1K/per-1M unit-basis recognition and explicit quantity handling;
- deterministic currency qualification with non-USD/missing currency remaining non-comparable to the USD token filter;
- corrected free-evidence derivation so unrelated zero-priced image/request dimensions do not mark paid token pricing free;
- bounded pricing-variant representation and ingestion with deterministic default selection and fail-safe ambiguity handling;
- Model Information now consumes the normalized Core comparability contract rather than applying a `UnitQuantity ?? 1` heuristic;
- focused pricing and Model Information regression coverage;
- owning architecture and off-work plan documentation aligned with the implemented boundary.

## Explicit exclusions

Not implemented in this slice:

- built-in provider catalog expansion (Slice 2);
- runtime token-usage accounting (Slice 3);
- Phase 1.30 metrics/budgets/OpenTelemetry;
- Agent target-selection changes;
- new durable Model resource;
- background pricing refresh/scraping;
- FX conversion;
- unrelated UI/control cleanup;
- roadmap advancement.

## Verification / remediation

Developer compile diagnostics received after the initial Slice 1 implementation included:
- `ProviderModelPrice.Value` reference errors in `ProviderDiscoveryContracts.cs`;
- `IReadOnlyList<ProviderModelPrice>` to `IList<ProviderModelPrice>` construction error in `ProviderDiscoveryContracts.cs`;
- missing `TryGetUnitQuantity` and `IsKnownBillingDimension` helpers in `OpenAICompatibleProviderAdapter.cs`.

Remediation applied:
- corrected nullable/reference handling for `ProviderModelPrice`;
- corrected collection construction;
- restored and bounded pricing-unit normalization helpers;
- preserved the intended conservative quantity semantics.

Verification remains pending.

Agent verification status:

- Source inspected/reviewed: Yes.
- Automated tests executed: No.
- Build executed: No.
- Example Host manually verified: No.

Required developer rerun targets:

- `ProviderPricingNormalizationTests`;
- affected `Phase116FollowUpTests` pricing / Model Information scenarios;
- affected `ProviderModelMetadataProviderTests` provider pricing regressions;
- full `Hive.Tests` suite;
- matching `Hive.Example.WinForms` Model Information scenario/manual verification.

Do not claim Slice 1 closure until developer results are received and the required verification passes. Any in-scope failure must be recorded here as `VERIFICATION FAILED / REMEDIATION REQUIRED` before remediation changes.

## Closure gate

After successful developer verification, archive the verification evidence, update Current Status from actual results, close this slice, and leave only the current no-slice state before starting Slice 2.