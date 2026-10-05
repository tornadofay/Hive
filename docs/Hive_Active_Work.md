# Hive — Active Work

Status: IMPLEMENTATION IN PROGRESS

## Authorized slice

**Off-work provider completion — Slice 1: Pricing Normalization**

Explicit user authorization: begin the off-work provider plan now, starting with Slice 1 only.

## Checkpoint

Started from `main` at commit `affe2f1e4f468a58345c0a2e2f66f6fbaea8019e`.

## Scope

Implement only the pricing-normalization portion of the off-work provider completion plan:

- make source pricing quantity semantics explicit and provider-correct;
- remove implicit quantity fallback from comparable token-price calculation;
- establish deterministic currency qualification;
- correct free-evidence semantics so unrelated zero-cost billing dimensions do not mark a paid model free;
- establish bounded pricing-variant representation only to the extent required by current provider evidence and deterministic comparison;
- keep the canonical Model Information comparison at normalized USD per 1M input/output tokens;
- update focused automated coverage and required provider/model UI regression coverage;
- update owning architecture/documentation required by the implemented boundary.

## Explicit exclusions

Do not implement:

- built-in provider catalog expansion (Slice 2);
- runtime token-usage accounting (Slice 3);
- Phase 1.30 metrics/budgets/OpenTelemetry;
- Agent target-selection changes;
- new durable Model resource;
- background pricing refresh/scraping;
- FX conversion;
- unrelated UI/control cleanup;
- roadmap advancement.

## Verification boundary

Required developer verification for this slice:

- focused pricing/Model Information tests;
- affected provider discovery regressions;
- full `Hive.Tests` suite after the focused boundary is stable;
- matching `Hive.Example.WinForms` Model Information scenario/manual verification where applicable.

The agent must not claim builds/tests/manual runs that were not actually performed.

## Closure gate

Slice 1 remains active until its implementation, focused tests, full-suite verification, required Example Host verification, and documentation review are complete. After developer results are received and pass, archive verification evidence and close this slice before starting Slice 2.